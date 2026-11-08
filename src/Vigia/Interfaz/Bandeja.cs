using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Dibujo = System.Drawing;

namespace Vigia.Interfaz;

/// <summary>
/// El icono de la bandeja es el control de Vigía: la franja no tiene botones
/// para no ocupar espacio encima del juego. El icono además se pinta del color
/// del estado, así que sirve de aviso incluso con la franja oculta.
/// </summary>
internal sealed partial class Bandeja : IDisposable
{
    private const string ClaveInicio = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string NombreInicio = "Vigia";

    private readonly Forms.NotifyIcon _icono;
    private readonly Dictionary<Nivel, Dibujo.Icon> _iconos;
    private readonly Configuracion _config;
    private readonly Franja _franja;
    private readonly Forms.ToolStripMenuItem _mostrar, _compacto, _fijar, _inicio;
    private readonly Dictionary<string, DateTime> _ultimoAviso = [];
    private Nivel _nivelActual = Nivel.SinDato;

    public event Action? SalirPedido;

    public Bandeja(Configuracion config, Franja franja)
    {
        _config = config;
        _franja = franja;
        _iconos = Enum.GetValues<Nivel>().ToDictionary(n => n, n => CrearIcono(Paleta.De(n).Color));

        _mostrar = new Forms.ToolStripMenuItem("Mostrar franja", null, (_, _) => AlternarVisibilidad())
        { ShortcutKeyDisplayString = "Ctrl+Shift+F11", Checked = true };
        _compacto = new Forms.ToolStripMenuItem("Modo mínimo", null, (_, _) => AlternarCompacto())
        { Checked = config.Compacto, ShortcutKeyDisplayString = "doble clic" };
        _fijar = new Forms.ToolStripMenuItem("Fijar y dejar pasar los clics", null, (_, _) => AlternarFijado())
        { Checked = config.Fijado };
        _inicio = new Forms.ToolStripMenuItem("Iniciar con Windows", null, (_, _) => AlternarInicio())
        { Checked = IniciaConWindows() };

        var opacidad = new Forms.ToolStripMenuItem("Opacidad");
        foreach (var valor in new[] { 1.0, 0.9, 0.75, 0.6 })
        {
            var item = new Forms.ToolStripMenuItem($"{valor * 100:0} %", null, (s, _) =>
            {
                _config.Opacidad = valor;
                _config.Guardar();
                _franja.AplicarOpacidad();
                foreach (Forms.ToolStripMenuItem o in opacidad.DropDownItems) o.Checked = o == s;
            })
            { Checked = Math.Abs(config.Opacidad - valor) < 0.01 };
            opacidad.DropDownItems.Add(item);
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.AddRange(
        [
            _mostrar, _compacto, _fijar, opacidad,
            new Forms.ToolStripMenuItem("Volver a la pantalla principal", null, (_, _) => _franja.VolverAPantallaPrincipal()),
            new Forms.ToolStripSeparator(),
            _inicio,
            new Forms.ToolStripMenuItem("Editar umbrales…", null, (_, _) => AbrirConfiguracion()),
            new Forms.ToolStripSeparator(),
            new Forms.ToolStripMenuItem("Salir", null, (_, _) => SalirPedido?.Invoke()),
        ]);

        _icono = new Forms.NotifyIcon
        {
            Icon = _iconos[Nivel.SinDato],
            Text = "Vigía",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icono.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) AlternarVisibilidad(); };

        _franja.AtajoPresionado += AlternarVisibilidad;
        _franja.ModoCambiado += () => _compacto.Checked = _config.Compacto;
    }

    public void PrimerUso()
    {
        _icono.ShowBalloonTip(6000, "Vigía está en la bandeja",
            "Arrastra la franja a otra pantalla o al otro borde. Doble clic: modo mínimo. Ctrl+Shift+F11: mostrar u ocultar. Clic derecho en este icono: opciones.",
            Forms.ToolTipIcon.None);
    }

    public void Actualizar(Sensores.Lectura l, Diagnostico d)
    {
        if (d.Nivel != _nivelActual)
        {
            _nivelActual = d.Nivel;
            _icono.Icon = _iconos[d.Nivel];
        }
        // El texto del icono tiene un tope de 127 caracteres.
        string texto = $"Vigía · {d.Mensaje}";
        _icono.Text = texto.Length > 127 ? texto[..127] : texto;

        // Aviso de Windows solo al entrar en crítico, y no más de uno cada 5
        // minutos por el mismo motivo. Con «No molestar al jugar» activo,
        // Windows lo guarda en el centro de notificaciones sin tapar el juego.
        if (d.Nivel == Nivel.Critico && _config.AvisarEnCritico)
        {
            string motivo = d.Mensaje.Split(' ')[0];
            if (!_ultimoAviso.TryGetValue(motivo, out var ultimo) || DateTime.Now - ultimo > TimeSpan.FromMinutes(5))
            {
                _ultimoAviso[motivo] = DateTime.Now;
                string valores = $"GPU {l.Gpu?.Temperatura:0} °C · CPU {l.Cpu.Temperatura:0} °C · RAM {l.Ram.Porcentaje:0} %";
                _icono.ShowBalloonTip(5000, d.Mensaje, valores, Forms.ToolTipIcon.Warning);
            }
        }
    }

    private void AlternarVisibilidad()
    {
        if (_franja.IsVisible) _franja.Hide();
        else _franja.Show();
        _mostrar.Checked = _franja.IsVisible;
    }

    private void AlternarCompacto()
    {
        _config.Compacto = !_config.Compacto;
        _config.Guardar();
        _franja.AplicarModo();
        _compacto.Checked = _config.Compacto;
    }

    private void AlternarFijado()
    {
        _config.Fijado = !_config.Fijado;
        _config.Guardar();
        _franja.AplicarEstilos();
        _fijar.Checked = _config.Fijado;
    }

    private static bool IniciaConWindows()
    {
        using var clave = Registry.CurrentUser.OpenSubKey(ClaveInicio);
        return clave?.GetValue(NombreInicio) is not null;
    }

    private void AlternarInicio()
    {
        using var clave = Registry.CurrentUser.CreateSubKey(ClaveInicio);
        if (IniciaConWindows()) clave.DeleteValue(NombreInicio, false);
        else clave.SetValue(NombreInicio, $"\"{Environment.ProcessPath}\"");
        _inicio.Checked = IniciaConWindows();
    }

    private void AbrirConfiguracion()
    {
        _config.Guardar();
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{Configuracion.Ruta}\"") { UseShellExecute = true });
    }

    /// <summary>Un círculo del color del estado; se dibuja en vez de traer archivos .ico.</summary>
    private static Dibujo.Icon CrearIcono(System.Windows.Media.Color color)
    {
        using var mapa = new Dibujo.Bitmap(32, 32);
        using (var g = Dibujo.Graphics.FromImage(mapa))
        {
            g.SmoothingMode = Dibujo.Drawing2D.SmoothingMode.AntiAlias;
            using var fondo = new Dibujo.SolidBrush(Dibujo.Color.FromArgb(0x14, 0x17, 0x1C));
            using var punto = new Dibujo.SolidBrush(Dibujo.Color.FromArgb(color.R, color.G, color.B));
            g.FillEllipse(fondo, 1, 1, 30, 30);
            g.FillEllipse(punto, 8, 8, 16, 16);
        }
        nint h = mapa.GetHicon();
        // GetHicon crea un handle que Icon no libera solo: se clona y se
        // destruye el original para no filtrar handles GDI.
        var icono = (Dibujo.Icon)Dibujo.Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        return icono;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint icono);

    public void Dispose()
    {
        _icono.Visible = false;
        _icono.Dispose();
        foreach (var i in _iconos.Values) i.Dispose();
    }
}
