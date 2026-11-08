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
    private Dictionary<Nivel, Dibujo.Icon> _iconos;
    private readonly Configuracion _config;
    private readonly Franja _franja;
    private readonly Forms.ToolStripMenuItem _mostrar, _fijar, _inicio;
    private readonly Dictionary<string, DateTime> _ultimoAviso = [];
    private Nivel _nivelActual = Nivel.SinDato;

    public event Action? SalirPedido;
    public event Action? PersonalizarPedido;

    public Bandeja(Configuracion config, Franja franja)
    {
        _config = config;
        _franja = franja;
        _iconos = CrearIconos();

        _mostrar = new Forms.ToolStripMenuItem("Mostrar franja", null, (_, _) => AlternarVisibilidad())
        { ShortcutKeyDisplayString = "Ctrl+Shift+F11", Checked = true };
        _fijar = new Forms.ToolStripMenuItem("Fijar y dejar pasar los clics", null, (_, _) => AlternarFijado())
        { Checked = config.Fijado };
        _inicio = new Forms.ToolStripMenuItem("Iniciar con Windows", null, (_, _) => AlternarInicio())
        { Checked = IniciaConWindows() };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.AddRange(
        [
            new Forms.ToolStripMenuItem("Personalizar…", null, (_, _) => PersonalizarPedido?.Invoke())
            { ShortcutKeyDisplayString = "doble clic", Font = new Dibujo.Font(menu.Font, Dibujo.FontStyle.Bold) },
            _mostrar, _fijar,
            new Forms.ToolStripMenuItem("Volver a la pantalla principal", null, (_, _) => _franja.VolverAPantallaPrincipal()),
            new Forms.ToolStripSeparator(),
            _inicio,
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
    }

    private Dictionary<Nivel, Dibujo.Icon> CrearIconos() =>
        Enum.GetValues<Nivel>().ToDictionary(n => n, n => CrearIcono(n == Nivel.SinDato ? null : Paleta.De(n).Color));

    /// <summary>Al cambiar los colores de aviso (p. ej. a los de daltonismo), el icono los sigue.</summary>
    public void RefrescarIconos()
    {
        var viejos = _iconos;
        _iconos = CrearIconos();
        _icono.Icon = _iconos[_nivelActual];
        foreach (var i in viejos.Values) i.Dispose();
    }

    public void PrimerUso()
    {
        _icono.ShowBalloonTip(6000, "Vigía está en la bandeja",
            "Arrastra la franja a cualquier borde o pantalla. Doble clic: personalizar. Ctrl+Shift+F11: mostrar u ocultar.",
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

    /// <summary>
    /// El logo de la marca con el visto pintado del color del estado: se
    /// reconoce como Vigía entre los demás iconos de la bandeja y sigue
    /// avisando aunque la franja esté oculta. Un punto encima tapaba justo el
    /// visto, que es lo que identifica el logo. Sin dato aún, el logo tal cual.
    /// </summary>
    private static Dibujo.Icon CrearIcono(System.Windows.Media.Color? estado)
    {
        var recurso = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Recursos/vigia.ico"));
        using var logo = new Dibujo.Icon(recurso!.Stream, 32, 32);
        using var mapa = logo.ToBitmap();
        if (estado is { } c) PintarVisto(mapa, c);
        nint h = mapa.GetHicon();
        // GetHicon crea un handle que Icon no libera solo: se clona y se
        // destruye el original para no filtrar handles GDI.
        var icono = (Dibujo.Icon)Dibujo.Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        return icono;
    }

    /// <summary>
    /// Los píxeles del visto son mezclas entre el turquesa (#2DD4BF) y el azul
    /// de la baldosa (#0A2B45) por el suavizado de bordes. Se mide cuánto
    /// turquesa tiene cada uno por la diferencia verde − rojo (167 en el
    /// turquesa, 33 en el azul, ~6 en el blanco de las letras, que así no se
    /// toca) y se reemplaza por la misma mezcla con el color del estado.
    /// </summary>
    private static void PintarVisto(Dibujo.Bitmap mapa, System.Windows.Media.Color estado)
    {
        const double Fondo = 33, Turquesa = 167;
        for (int y = 0; y < mapa.Height; y++)
        for (int x = 0; x < mapa.Width; x++)
        {
            var p = mapa.GetPixel(x, y);
            double mezcla = (p.G - p.R - Fondo) / (Turquesa - Fondo);
            if (mezcla <= 0.2 || p.A == 0) continue;
            mezcla = Math.Min(1, mezcla);
            int Canal(byte fondo, byte color) => (int)Math.Round(fondo + mezcla * (color - fondo));
            mapa.SetPixel(x, y, Dibujo.Color.FromArgb(p.A, Canal(0x0A, estado.R), Canal(0x2B, estado.G), Canal(0x45, estado.B)));
        }
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
