using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Vigia.Nativo;
using Vigia.Sensores;

namespace Vigia.Interfaz;

/// <summary>
/// Una franja delgada pegada al borde superior o inferior de una pantalla, a
/// todo lo ancho. Se arrastra a otra pantalla o al otro borde y, al soltarla,
/// se acomoda sola.
///
/// Nunca toma el foco (WS_EX_NOACTIVATE): arrastrarla o hacerle doble clic no
/// saca al juego de primer plano, que es lo que soltaría el mouse capturado o
/// minimizaría un juego en pantalla completa.
/// </summary>
public partial class Franja : Window
{
    private const double AltoDip = 30;
    private const int CapacidadHistorial = 120; // 2 minutos a una lectura por segundo
    private const int IdAtajo = 1;

    private readonly Configuracion _config;
    private readonly Queue<double> _historialGpu = new(CapacidadHistorial);
    private readonly Queue<double> _historialCpu = new(CapacidadHistorial);
    private readonly Brush _lineaNormal;
    private Sistema.ProcesarEventoVentana? _alCambiarPrimerPlano; // campo: si el GC lo recoge, el gancho nativo se cae
    private nint _gancho;
    private double _maxGpu, _maxCpu;
    private nint _hwnd;

    public event Action? AtajoPresionado;
    public event Action? ModoCambiado;

    public Franja(Configuracion config)
    {
        _config = config;
        InitializeComponent();
        _lineaNormal = (Brush)FindResource("Separador");
        AplicarOpacidad();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        AplicarEstilos();
        Acomodar();
        HwndSource.FromHwnd(_hwnd)?.AddHook(ProcesarMensaje);

        // Ctrl+Shift+F11 muestra u oculta la franja desde dentro del juego.
        Sistema.RegisterHotKey(_hwnd, IdAtajo, Sistema.MOD_CONTROL | Sistema.MOD_SHIFT | Sistema.MOD_NOREPEAT, Sistema.VK_F11);

        // Algunos juegos sin bordes se ponen «siempre encima» al recibir el
        // foco y tapan la franja. En vez de reafirmarla con un reloj (medido:
        // un tercio de la CPU de Vigía), se reafirma solo cuando cambia la
        // ventana activa, que es cuando eso puede pasar.
        _alCambiarPrimerPlano = (_, _, _, _, _, _, _) => Sistema.TraerAlFrente(_hwnd);
        _gancho = Sistema.SetWinEventHook(Sistema.EVENT_SYSTEM_FOREGROUND, Sistema.EVENT_SYSTEM_FOREGROUND,
            0, _alCambiarPrimerPlano, 0, 0, 0);

        // Conectar o desconectar una pantalla, o cambiar la resolución.
        SystemEvents.DisplaySettingsChanged += AlCambiarPantallas;
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= AlCambiarPantallas;
        if (_gancho != 0) Sistema.UnhookWinEvent(_gancho);
        Sistema.UnregisterHotKey(_hwnd, IdAtajo);
        base.OnClosed(e);
    }

    private void AlCambiarPantallas(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Acomodar);

    // Al pasar a una pantalla con otra escala, WPF redimensiona la ventana
    // por su cuenta con la escala vieja; se vuelve a acomodar después.
    protected override void OnDpiChanged(DpiScale anterior, DpiScale nueva)
    {
        base.OnDpiChanged(anterior, nueva);
        Dispatcher.BeginInvoke(Acomodar, System.Windows.Threading.DispatcherPriority.Background);
    }

    private nint ProcesarMensaje(nint hwnd, int msg, nint wParam, nint lParam, ref bool manejado)
    {
        if (msg == Sistema.WM_HOTKEY && wParam == IdAtajo)
        {
            AtajoPresionado?.Invoke();
            manejado = true;
        }
        return 0;
    }

    /// <summary>Lleva la franja al borde guardado, a todo el ancho del área de trabajo.</summary>
    public void Acomodar()
    {
        if (_hwnd == 0) return;
        // (0,0) siempre cae en la pantalla principal.
        var punto = new Sistema.Punto { X = _config.PantallaX ?? 0, Y = _config.PantallaY ?? 0 };
        var (area, escala) = Sistema.MonitorEn(punto);
        int alto = (int)Math.Round(AltoDip * escala);
        bool abajo = _config.Borde == "Abajo";

        Sistema.Colocar(_hwnd, new Sistema.Rect
        {
            Izquierda = area.Izquierda,
            Derecha = area.Derecha,
            Arriba = abajo ? area.Abajo - alto : area.Arriba,
            Abajo = abajo ? area.Abajo : area.Arriba + alto,
        });
        LineaEstado.VerticalAlignment = abajo ? VerticalAlignment.Top : VerticalAlignment.Bottom;
    }

    public void VolverAPantallaPrincipal()
    {
        _config.PantallaX = _config.PantallaY = null;
        _config.Borde = "Arriba";
        _config.Guardar();
        Acomodar();
    }

    /// <summary>
    /// Fijada = deja pasar los clics al juego que está debajo
    /// (WS_EX_TRANSPARENT). Mientras está fijada no se arrastra: se suelta
    /// desde el icono de la bandeja.
    /// </summary>
    public void AplicarEstilos()
    {
        if (_hwnd == 0) return;
        nint estilo = Sistema.GetWindowLongPtr(_hwnd, Sistema.GWL_EXSTYLE);
        estilo |= Sistema.WS_EX_NOACTIVATE | Sistema.WS_EX_TOOLWINDOW;
        estilo = _config.Fijado ? estilo | Sistema.WS_EX_TRANSPARENT : estilo & ~(nint)Sistema.WS_EX_TRANSPARENT;
        Sistema.SetWindowLongPtr(_hwnd, Sistema.GWL_EXSTYLE, estilo);
    }

    public void AplicarOpacidad() =>
        Fondo.Background = new SolidColorBrush(Color.FromArgb((byte)(255 * _config.Opacidad), 0x14, 0x17, 0x1C));

    private void AlPresionar(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1 || _config.Fijado) return;
        DragMove();

        // Donde se suelta decide la pantalla, y la mitad de esa pantalla, el borde.
        Sistema.GetCursorPos(out var cursor);
        var (area, _) = Sistema.MonitorEn(cursor);
        _config.PantallaX = (area.Izquierda + area.Derecha) / 2;
        _config.PantallaY = (area.Arriba + area.Abajo) / 2;
        _config.Borde = cursor.Y > _config.PantallaY ? "Abajo" : "Arriba";
        _config.Guardar();
        Acomodar();
    }

    private void AlDobleClic(object sender, MouseButtonEventArgs e)
    {
        _config.Compacto = !_config.Compacto;
        _config.Guardar();
        AplicarModo();
        ModoCambiado?.Invoke();
    }

    /// <summary>Repinta con la última lectura para que el cambio de modo se vea al instante y no al segundo siguiente.</summary>
    public void AplicarModo()
    {
        if (_ultima is var (l, d)) Actualizar(l, d);
    }

    private (Lectura, Diagnostico)? _ultima;

    public void Actualizar(Lectura l, Diagnostico d)
    {
        _ultima = (l, d);
        bool minimo = _config.Compacto;
        var colorEstado = Paleta.De(d.Nivel);
        PuntoEstado.Fill = colorEstado;
        TextoEstado.Text = d.Mensaje;
        TextoEstado.Foreground = d.Nivel == Nivel.Normal ? Paleta.Texto : colorEstado;
        LineaEstado.Background = d.Nivel >= Nivel.Atencion ? colorEstado : _lineaNormal;

        if (l.Fps is { } fps)
        {
            string bajo = fps.UnoPorCientoBajo is double b ? $"1 % bajo {b:0} · " : "";
            SegFps.Mostrar($"{fps.Fps:0}", Paleta.Texto, null, bajo + fps.Proceso, minimo);
        }
        else
        {
            SegFps.Mostrar("—", Paleta.SinDato, null, l.ErrorFps ?? "sin juego en pantalla", minimo);
        }

        if (l.Gpu is { } gpu)
        {
            Agregar(_historialGpu, gpu.Temperatura);
            if (gpu.Temperatura > _maxGpu) _maxGpu = gpu.Temperatura.Value;

            var detalle = new List<string>();
            if (gpu.Temperatura is null) detalle.Add("en reposo");
            if (_maxGpu > 0) detalle.Add($"máx {_maxGpu:0}°");
            if (gpu.VramUsadaGb is double usada && gpu.VramTotalGb is double total) detalle.Add($"VRAM {usada:0.0}/{total:0.0} GB");
            if (gpu.VentiladorRpm is uint rpm) detalle.Add($"{rpm} rpm");

            SegGpu.Mostrar(Grados(gpu.Temperatura), Paleta.ValorDe(d.Gpu), Porcentaje(gpu.Carga), string.Join(" · ", detalle), minimo);
            SegGpu.MostrarHistorial(_historialGpu, _config.TemperaturaGpu.Atencion, Paleta.De(d.Gpu), CapacidadHistorial, minimo);
        }
        else
        {
            SegGpu.Mostrar("—", Paleta.SinDato, null, "no encontrada", minimo);
        }

        var cpu = l.Cpu;
        Agregar(_historialCpu, cpu.Temperatura);
        if (cpu.Temperatura > _maxCpu) _maxCpu = cpu.Temperatura.Value;
        var detalleCpu = new List<string>();
        if (_maxCpu > 0) detalleCpu.Add($"máx {_maxCpu:0}°");
        if (cpu.FrecuenciaMhz is double mhz) detalleCpu.Add($"{mhz / 1000:0.0} GHz");
        SegCpu.Mostrar(Grados(cpu.Temperatura), Paleta.ValorDe(d.Cpu), Porcentaje(cpu.Carga), string.Join(" · ", detalleCpu), minimo);
        SegCpu.MostrarHistorial(_historialCpu, _config.TemperaturaCpu.Atencion, Paleta.De(d.Cpu), CapacidadHistorial, minimo);

        SegRam.Mostrar($"{l.Ram.Porcentaje:0} %", Paleta.ValorDe(d.Ram), null, $"{l.Ram.UsadaGb:0.0} / {l.Ram.TotalGb:0} GB", minimo);
    }

    private static string Grados(double? t) => t is double v ? $"{v:0}°" : "—";
    private static string? Porcentaje(double? p) => p is double v ? $"{v:0} %" : null;

    private static void Agregar(Queue<double> historial, double? valor)
    {
        if (valor is not double v) return;
        if (historial.Count == CapacidadHistorial) historial.Dequeue();
        historial.Enqueue(v);
    }
}
