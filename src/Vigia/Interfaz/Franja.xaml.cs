using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Vigia.Nativo;
using Vigia.Sensores;
using Borde = Vigia.Nativo.BarraDeAplicacion.Borde;

namespace Vigia.Interfaz;

/// <summary>
/// Una franja pegada a un borde de una pantalla: a todo lo ancho arriba o
/// abajo, a todo lo alto a la izquierda o a la derecha. Se arrastra a otra
/// pantalla u otro borde y, al soltarla, se pega al borde más cercano.
///
/// Nunca toma el foco (WS_EX_NOACTIVATE): arrastrarla no saca al juego de
/// primer plano, que es lo que soltaría el mouse capturado o minimizaría un
/// juego en pantalla completa. El doble clic sí abre una ventana normal
/// («Personalizar»), porque es una acción que se pide a propósito.
/// </summary>
public partial class Franja : Window
{
    private const double GrosorHorizontalDip = 30;
    private const double AnchoVerticalDip = 172;
    private const int CapacidadHistorial = 120; // 2 minutos a una lectura por segundo
    private const int IdAtajo = 1;

    private readonly Configuracion _config;
    private readonly Queue<double> _historialGpu = new(CapacidadHistorial);
    private readonly Queue<double> _historialCpu = new(CapacidadHistorial);
    private Sistema.ProcesarEventoVentana? _alCambiarPrimerPlano; // campo: si el GC lo recoge, el gancho nativo se cae
    private nint _gancho;
    private double _maxGpu, _maxCpu;
    private nint _hwnd;
    private bool _reservada;
    private long _ultimoAcomodo;
    private (Lectura, Diagnostico)? _ultima;

    public event Action? AtajoPresionado;
    public event Action? PersonalizarPedido;

    public Franja(Configuracion config)
    {
        _config = config;
        InitializeComponent();
        AplicarConfiguracion();
        IsVisibleChanged += (_, _) => Acomodar();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        AplicarEstilos();
        HwndSource.FromHwnd(_hwnd)?.AddHook(ProcesarMensaje);
        Acomodar();

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
        QuitarReserva();
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
        else if (msg == BarraDeAplicacion.MensajeAviso && wParam == BarraDeAplicacion.ABN_POSCHANGED)
        {
            // Otra barra cambió (la de tareas, por ejemplo). Nuestro propio
            // acomodo también puede disparar este aviso: se ignora si llega
            // justo después, o las dos barras se reacomodarían sin fin.
            if (Stopwatch.GetElapsedTime(_ultimoAcomodo).TotalMilliseconds > 300)
                Dispatcher.BeginInvoke(Acomodar);
        }
        return 0;
    }

    private Borde BordeActual => _config.Borde switch
    {
        "Abajo" => Borde.Abajo,
        "Izquierda" => Borde.Izquierda,
        "Derecha" => Borde.Derecha,
        _ => Borde.Arriba,
    };

    /// <summary>
    /// Pega la franja a su borde. Con «reservar espacio», Windows le aparta
    /// esa franja de pantalla como a la barra de tareas y las ventanas
    /// maximizadas terminan donde empieza; sin eso, la franja flota encima.
    /// </summary>
    public void Acomodar()
    {
        if (_hwnd == 0) return;

        // Oculta no reserva nada: si no, quedaría un hueco vacío en el borde.
        bool reservar = _config.ReservarEspacio && IsVisible;
        if (!reservar) QuitarReserva();

        // (0,0) siempre cae en la pantalla principal.
        var punto = new Sistema.Punto { X = _config.PantallaX ?? 0, Y = _config.PantallaY ?? 0 };
        var (completo, trabajo, escalaMonitor) = Sistema.MonitorEn(punto);
        double dip = _config.EsVertical ? AnchoVerticalDip : GrosorHorizontalDip;
        int grosor = (int)Math.Round(dip * escalaMonitor * _config.Escala);

        Sistema.Rect r;
        if (reservar)
        {
            if (!_reservada)
            {
                BarraDeAplicacion.Registrar(_hwnd);
                _reservada = true;
            }
            r = BarraDeAplicacion.Reservar(_hwnd, BordeActual, completo, grosor);
        }
        else
        {
            r = trabajo;
            BarraDeAplicacion.Recortar(ref r, BordeActual, grosor);
        }

        _ultimoAcomodo = Stopwatch.GetTimestamp();
        Sistema.Colocar(_hwnd, r);
    }

    /// <summary>Devuelve el espacio a las demás ventanas. También se llama si Vigía cae, para no dejar un hueco.</summary>
    public void QuitarReserva()
    {
        if (!_reservada || _hwnd == 0) return;
        BarraDeAplicacion.Quitar(_hwnd);
        _reservada = false;
    }

    public void VolverAPantallaPrincipal()
    {
        _config.PantallaX = _config.PantallaY = null;
        _config.Borde = "Arriba";
        _config.Guardar();
        AplicarConfiguracion();
    }

    /// <summary>
    /// Fijada = deja pasar los clics a lo que está debajo (WS_EX_TRANSPARENT).
    /// Mientras está fijada no se arrastra ni abre «Personalizar»: se suelta
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

    /// <summary>Todo lo que depende de la configuración: colores, tamaño, orientación, qué se muestra y dónde va.</summary>
    public void AplicarConfiguracion()
    {
        var f = Paleta.Fondo;
        Fondo.Background = new SolidColorBrush(Color.FromArgb((byte)(255 * _config.Opacidad), f.R, f.G, f.B));
        Escala.ScaleX = Escala.ScaleY = _config.Escala;
        Orientar();
        Acomodar();
        if (_ultima is var (l, d)) Actualizar(l, d);
    }

    /// <summary>
    /// Arriba y abajo: una fila (estado de ancho fijo y los componentes
    /// repartidos). A los costados: una columna con un bloque por componente.
    /// Lo que se oculta desde «Personalizar» deja de ocupar lugar.
    /// </summary>
    private void Orientar()
    {
        bool vertical = _config.EsVertical;
        var piezas = new (FrameworkElement Pieza, bool Visible)[]
        {
            (Estado, true),
            (SegFps, _config.MostrarFps),
            (SegGpu, _config.MostrarGpu),
            (SegCpu, _config.MostrarCpu),
            (SegRam, _config.MostrarRam),
        };

        Contenido.ColumnDefinitions.Clear();
        Contenido.RowDefinitions.Clear();
        Contenido.Margin = vertical ? new Thickness(12, 14, 12, 14) : new Thickness(14, 0, 14, 0);

        for (int i = 0; i < piezas.Length; i++)
        {
            var (pieza, visible) = piezas[i];
            pieza.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            // El estado cambia de largo («Todo en orden» / «GPU y CPU muy
            // calientes»): con ancho fijo, lo demás no salta de lugar.
            var tamano = !visible ? new GridLength(0)
                : i == 0 ? (vertical ? GridLength.Auto : new GridLength(230))
                : new GridLength(1, GridUnitType.Star);

            if (vertical)
            {
                Contenido.RowDefinitions.Add(new RowDefinition { Height = tamano });
                Grid.SetRow(pieza, i);
                Grid.SetColumn(pieza, 0);
            }
            else
            {
                Contenido.ColumnDefinitions.Add(new ColumnDefinition { Width = tamano });
                Grid.SetColumn(pieza, i);
                Grid.SetRow(pieza, 0);
            }

            if (pieza is Segmento s)
            {
                s.Orientar(vertical);
                s.Preferencias(_config.Compacto, _config.MostrarTendencias);
            }
        }
        Estado.Margin = vertical ? new Thickness(0, 0, 0, 10) : new Thickness(0);

        // La línea de estado va en el lado que mira hacia la pantalla.
        LineaEstado.Width = vertical ? 2 : double.NaN;
        LineaEstado.Height = vertical ? double.NaN : 2;
        LineaEstado.HorizontalAlignment = BordeActual switch
        {
            Borde.Izquierda => HorizontalAlignment.Right,
            Borde.Derecha => HorizontalAlignment.Left,
            _ => HorizontalAlignment.Stretch,
        };
        LineaEstado.VerticalAlignment = BordeActual switch
        {
            Borde.Arriba => VerticalAlignment.Bottom,
            Borde.Abajo => VerticalAlignment.Top,
            _ => VerticalAlignment.Stretch,
        };
    }

    private void AlPresionar(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1 || _config.Fijado) return;
        DragMove();

        // Donde se suelta decide la pantalla, y el borde más cercano al
        // cursor dentro de esa pantalla, el lado.
        Sistema.GetCursorPos(out var c);
        var (m, _, _) = Sistema.MonitorEn(c);
        var distancias = new (string Borde, int Distancia)[]
        {
            ("Arriba", c.Y - m.Arriba),
            ("Abajo", m.Abajo - c.Y),
            ("Izquierda", c.X - m.Izquierda),
            ("Derecha", m.Derecha - c.X),
        };
        _config.Borde = distancias.MinBy(d => d.Distancia).Borde;
        _config.PantallaX = (m.Izquierda + m.Derecha) / 2;
        _config.PantallaY = (m.Arriba + m.Abajo) / 2;
        _config.Guardar();
        AplicarConfiguracion();
    }

    private void AlDobleClic(object sender, MouseButtonEventArgs e) => PersonalizarPedido?.Invoke();

    public void Actualizar(Lectura l, Diagnostico d)
    {
        _ultima = (l, d);
        var colorEstado = Paleta.De(d.Nivel);
        PuntoEstado.Fill = colorEstado;
        TextoEstado.Text = d.Mensaje;
        TextoEstado.Foreground = d.Nivel == Nivel.Normal ? Paleta.Texto : colorEstado;
        LineaEstado.Background = d.Nivel >= Nivel.Atencion ? colorEstado : Paleta.Separador;

        if (l.Fps is { } fps)
        {
            string bajo = fps.UnoPorCientoBajo is double b ? $"1 % bajo {b:0} · " : "";
            SegFps.Mostrar($"{fps.Fps:0}", Paleta.Texto, null, bajo + fps.Proceso);
        }
        else
        {
            SegFps.Mostrar("—", Paleta.SinDato, null, l.ErrorFps ?? "sin juego en pantalla");
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

            SegGpu.Mostrar(Grados(gpu.Temperatura), Paleta.ValorDe(d.Gpu), Porcentaje(gpu.Carga), string.Join(" · ", detalle));
            SegGpu.MostrarHistorial(_historialGpu, _config.TemperaturaGpu.Atencion, Paleta.De(d.Gpu), CapacidadHistorial);
        }
        else
        {
            SegGpu.Mostrar("—", Paleta.SinDato, null, "no encontrada");
        }

        var cpu = l.Cpu;
        Agregar(_historialCpu, cpu.Temperatura);
        if (cpu.Temperatura > _maxCpu) _maxCpu = cpu.Temperatura.Value;
        var detalleCpu = new List<string>();
        if (_maxCpu > 0) detalleCpu.Add($"máx {_maxCpu:0}°");
        if (cpu.FrecuenciaMhz is double mhz) detalleCpu.Add($"{mhz / 1000:0.0} GHz");
        SegCpu.Mostrar(Grados(cpu.Temperatura), Paleta.ValorDe(d.Cpu), Porcentaje(cpu.Carga), string.Join(" · ", detalleCpu));
        SegCpu.MostrarHistorial(_historialCpu, _config.TemperaturaCpu.Atencion, Paleta.De(d.Cpu), CapacidadHistorial);

        SegRam.Mostrar($"{l.Ram.Porcentaje:0} %", Paleta.ValorDe(d.Ram), null, $"{l.Ram.UsadaGb:0.0} / {l.Ram.TotalGb:0} GB");
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
