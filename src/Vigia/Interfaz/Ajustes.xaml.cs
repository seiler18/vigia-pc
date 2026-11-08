using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Vigia.Interfaz;

/// <summary>
/// «Personalizar»: se abre con doble clic en la franja o desde la bandeja.
/// Cada cambio se ve al instante en la franja (evento Cambio) y se guarda
/// en el archivo al cerrar, no en cada paso de un deslizador.
/// </summary>
public partial class Ajustes : Window
{
    private static readonly (string Nombre, string Hex)[] Fondos =
    [
        ("Grafito", "#14171C"), ("Negro", "#000000"), ("Azul noche", "#0F172A"),
        ("Violeta", "#1E1B2E"), ("Bosque", "#10241B"), ("Claro", "#F4F4F5"),
    ];

    private static readonly (string Nombre, string Hex)[] Textos =
    [
        ("Blanco suave", "#E8EAED"), ("Blanco", "#FFFFFF"), ("Arena", "#FDE68A"),
        ("Menta", "#A7F3D0"), ("Oscuro", "#1F2937"),
    ];

    private readonly Configuracion _config;
    private bool _cargando;

    public event Action? Cambio;

    public Ajustes(Configuracion config)
    {
        _config = config;
        InitializeComponent();
        CrearMuestras(MuestrasFondo, Fondos, hex => _config.ColorFondo = hex);
        CrearMuestras(MuestrasTexto, Textos, hex => _config.ColorTexto = hex);
        CrearUmbral(1, "GPU", () => _config.TemperaturaGpu, 50, 110, "°");
        CrearUmbral(2, "CPU", () => _config.TemperaturaCpu, 50, 110, "°");
        CrearUmbral(3, "RAM", () => _config.Ram, 50, 100, " %");
        Cargar();
        SeccionFps.Visibility = Sensores.PermisoFps.Tiene() ? Visibility.Collapsed : Visibility.Visible;
        Closed += (_, _) => _config.Guardar();
    }

    private async void AlActivarFps(object sender, RoutedEventArgs e)
    {
        BotonFps.IsEnabled = false;
        var resultado = await Sensores.PermisoFps.ConcederAsync();
        switch (resultado)
        {
            case Sensores.PermisoFps.Resultado.Listo:
                // El permiso entra en el token de la sesión: hasta volver a
                // entrar, Windows sigue sin dejar abrir la sesión ETW.
                TextoFps.Text = "Listo. Cierra la sesión de Windows y vuelve a entrar para que los FPS aparezcan.";
                BotonFps.Visibility = Visibility.Collapsed;
                break;
            case Sensores.PermisoFps.Resultado.Cancelado:
                TextoFps.Text = "No se activó: Windows necesita tu confirmación para dar el permiso.";
                BotonFps.IsEnabled = true;
                break;
            default:
                TextoFps.Text = "No se pudo activar. Otra opción: abrir Vigía con clic derecho → «Ejecutar como administrador».";
                BotonFps.IsEnabled = true;
                break;
        }
    }

    /// <summary>
    /// Se abre centrada en la pantalla de la franja (donde se hizo el doble
    /// clic) y siempre encima: con un juego en primer plano, una ventana
    /// normal quedaba abierta detrás del juego, invisible.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var punto = new Nativo.Sistema.Punto { X = _config.PantallaX ?? 0, Y = _config.PantallaY ?? 0 };
        var (_, area, escala) = Nativo.Sistema.MonitorEn(punto);
        int altoArea = area.Abajo - area.Arriba;
        Height = Math.Min(Height, altoArea / escala - 40);
        int ancho = (int)(Width * escala), alto = (int)(Height * escala);
        Nativo.Sistema.Mover(new System.Windows.Interop.WindowInteropHelper(this).Handle,
            area.Izquierda + (area.Derecha - area.Izquierda - ancho) / 2,
            area.Arriba + (altoArea - alto) / 2);
        Activate();
    }

    /// <summary>Lleva la configuración a los controles sin disparar cambios.</summary>
    private void Cargar()
    {
        _cargando = true;
        foreach (RadioButton r in Bordes.Children) r.IsChecked = (string)r.Tag == _config.Borde;
        Reservar.IsChecked = _config.ReservarEspacio;
        Tamano.Value = Math.Round(_config.Escala * 100);
        Opacidad.Value = Math.Round(_config.Opacidad * 100);
        (_config.ColoresEstado == "Daltonismo" ? Daltonismo : Semaforo).IsChecked = true;
        VerFps.IsChecked = _config.MostrarFps;
        VerGpu.IsChecked = _config.MostrarGpu;
        VerCpu.IsChecked = _config.MostrarCpu;
        VerRam.IsChecked = _config.MostrarRam;
        VerTendencias.IsChecked = _config.MostrarTendencias;
        Minimo.IsChecked = _config.Compacto;
        AvisoWindows.IsChecked = _config.AvisarEnCritico;
        HexFondo.Text = _config.ColorFondo;
        HexTexto.Text = _config.ColorTexto;
        TamanoTexto.Text = $"{Tamano.Value:0} %";
        OpacidadTexto.Text = $"{Opacidad.Value:0} %";
        MarcarMuestras();
        _cargando = false;
    }

    private void Avisar()
    {
        if (_cargando) return;
        _config.Normalizada();
        Cambio?.Invoke();
    }

    private void AlElegirBorde(object sender, RoutedEventArgs e)
    {
        if (_cargando || sender is not RadioButton { Tag: string borde }) return;
        _config.Borde = borde;
        Avisar();
    }

    private void AlCambiar(object sender, RoutedEventArgs e)
    {
        if (_cargando) return;
        _config.ReservarEspacio = Reservar.IsChecked == true;
        _config.ColoresEstado = Daltonismo.IsChecked == true ? "Daltonismo" : "Semaforo";
        _config.MostrarFps = VerFps.IsChecked == true;
        _config.MostrarGpu = VerGpu.IsChecked == true;
        _config.MostrarCpu = VerCpu.IsChecked == true;
        _config.MostrarRam = VerRam.IsChecked == true;
        _config.MostrarTendencias = VerTendencias.IsChecked == true;
        _config.Compacto = Minimo.IsChecked == true;
        _config.AvisarEnCritico = AvisoWindows.IsChecked == true;
        Avisar();

        // Normalizada() vuelve a encender GPU y CPU si se apagó todo: que la
        // casilla lo refleje en vez de mentir.
        if (!_cargando && VerGpu.IsChecked != _config.MostrarGpu) Cargar();
    }

    private void AlDeslizar(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TamanoTexto is null || OpacidadTexto is null) return; // durante InitializeComponent
        TamanoTexto.Text = $"{Tamano.Value:0} %";
        OpacidadTexto.Text = $"{Opacidad.Value:0} %";
        if (_cargando) return;
        _config.Escala = Tamano.Value / 100;
        _config.Opacidad = Opacidad.Value / 100;
        Avisar();
    }

    private void CrearMuestras(WrapPanel panel, (string Nombre, string Hex)[] colores, Action<string> elegir)
    {
        foreach (var (nombre, hex) in colores)
        {
            var muestra = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand,
                ToolTip = nombre,
                Tag = hex,
            };
            muestra.MouseLeftButtonUp += (_, _) =>
            {
                elegir(hex);
                Cargar();
                Avisar();
            };
            panel.Children.Add(muestra);
        }
    }

    /// <summary>El color elegido lleva un aro marcado; los demás, uno tenue para que el negro y el blanco se vean sobre cualquier tema.</summary>
    private void MarcarMuestras()
    {
        var marcado = SystemColors.HighlightBrush;
        var tenue = new SolidColorBrush(Color.FromArgb(0x55, 0x80, 0x80, 0x80));
        foreach (Border b in MuestrasFondo.Children)
            b.BorderBrush = string.Equals((string)b.Tag, _config.ColorFondo, StringComparison.OrdinalIgnoreCase) ? marcado : tenue;
        foreach (Border b in MuestrasTexto.Children)
            b.BorderBrush = string.Equals((string)b.Tag, _config.ColorTexto, StringComparison.OrdinalIgnoreCase) ? marcado : tenue;
    }

    private void AlTeclaHex(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AlEscribirHex(sender, e);
    }

    private void AlEscribirHex(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox caja) return;
        string hex = caja.Text.Trim();
        if (!hex.StartsWith('#')) hex = "#" + hex;
        hex = hex.ToUpperInvariant();
        if (!Configuracion.EsColor(hex))
        {
            Cargar(); // vuelve a mostrar el color vigente
            return;
        }
        if (caja == HexFondo) _config.ColorFondo = hex;
        else _config.ColorTexto = hex;
        Cargar();
        Avisar();
    }

    /// <summary>
    /// Una fila por componente: deslizador de atención y de crítico. Si uno
    /// cruza al otro, lo empuja: un crítico por debajo de la atención no
    /// tiene sentido y la configuración lo rechazaría.
    /// </summary>
    private void CrearUmbral(int fila, string nombre, Func<Umbral> umbral, double min, double max, string unidad)
    {
        var etiqueta = new TextBlock { Text = nombre, VerticalAlignment = VerticalAlignment.Center };
        var atencion = new Slider { Minimum = min, Maximum = max, IsSnapToTickEnabled = true, TickFrequency = 1, Value = umbral().Atencion, Margin = new Thickness(0, 6, 0, 6) };
        var critico = new Slider { Minimum = min, Maximum = max, IsSnapToTickEnabled = true, TickFrequency = 1, Value = umbral().Critico, Margin = new Thickness(12, 6, 0, 6) };
        var textoAtencion = new TextBlock { Style = (Style)FindResource("Cifra") };
        var textoCritico = new TextBlock { Style = (Style)FindResource("Cifra") };

        void Refrescar()
        {
            textoAtencion.Text = $"{atencion.Value:0}{unidad}";
            textoCritico.Text = $"{critico.Value:0}{unidad}";
        }

        atencion.ValueChanged += (_, _) =>
        {
            if (critico.Value < atencion.Value) critico.Value = atencion.Value;
            umbral().Atencion = atencion.Value;
            Refrescar();
            Avisar();
        };
        critico.ValueChanged += (_, _) =>
        {
            if (atencion.Value > critico.Value) atencion.Value = critico.Value;
            umbral().Critico = critico.Value;
            Refrescar();
            Avisar();
        };
        Refrescar();

        var piezas = new UIElement[] { etiqueta, atencion, textoAtencion, critico, textoCritico };
        for (int i = 0; i < piezas.Length; i++)
        {
            Grid.SetRow(piezas[i], fila);
            Grid.SetColumn(piezas[i], i);
            Umbrales.Children.Add(piezas[i]);
        }
    }

    private void AlRestablecer(object sender, RoutedEventArgs e)
    {
        _config.RestablecerApariencia();
        Cargar();
        Cambio?.Invoke();
    }

    private void AlCerrar(object sender, RoutedEventArgs e) => Close();
}
