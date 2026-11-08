using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Vigia.Interfaz;

public partial class Segmento : UserControl
{
    // Escala fija para que la pendiente signifique lo mismo siempre: con una
    // escala automática, subir de 70 a 71 °C se vería como un salto.
    private const double EjeMin = 40, EjeMax = 100;

    private bool _minimo, _tendencias = true;

    public Segmento() => InitializeComponent();

    public string TituloTexto { set => Titulo.Text = value; }

    /// <summary>
    /// En fila (bordes de arriba y abajo):  GPU 87° ╌▂▃ 89 %  máx 87° · VRAM…
    /// En bloque (costados), para una columna angosta:
    ///   GPU          89 %
    ///   87°        ╌▂▃▅
    ///   máx 87° · VRAM…   (en varias líneas si no cabe)
    /// </summary>
    public void Orientar(bool vertical)
    {
        if (vertical)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            Raiz.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            Ubicar(Titulo, 0, 0, 2, new Thickness(0));
            Ubicar(Porcentaje, 0, 2, 3, new Thickness(0));
            Porcentaje.HorizontalAlignment = HorizontalAlignment.Right;
            Ubicar(Valor, 1, 0, 2, new Thickness(0, 2, 0, 0));
            Ubicar(Grafico, 1, 2, 3, new Thickness(8, 2, 0, 0));
            Grafico.HorizontalAlignment = HorizontalAlignment.Right;
            Grafico.Width = 70;
            Ubicar(Detalle, 2, 0, 5, new Thickness(0, 4, 0, 0));
            Detalle.TextWrapping = TextWrapping.Wrap;
            Valor.FontSize = 20;
        }
        else
        {
            HorizontalAlignment = HorizontalAlignment.Center;
            Raiz.ColumnDefinitions[1].Width = GridLength.Auto;
            Ubicar(Titulo, 0, 0, 1, new Thickness(0, 0, 6, 0));
            Ubicar(Valor, 0, 1, 1, new Thickness(0));
            Ubicar(Grafico, 0, 2, 1, new Thickness(8, 0, 0, 0));
            Grafico.HorizontalAlignment = HorizontalAlignment.Left;
            Grafico.Width = 56;
            Ubicar(Porcentaje, 0, 3, 1, new Thickness(8, 0, 0, 0));
            Porcentaje.HorizontalAlignment = HorizontalAlignment.Left;
            Ubicar(Detalle, 0, 4, 1, new Thickness(10, 0, 0, 0));
            Detalle.TextWrapping = TextWrapping.NoWrap;
            Valor.FontSize = 15;
        }
        LineaUmbral.X2 = Grafico.Width;
    }

    private static void Ubicar(UIElement e, int fila, int columna, int span, Thickness margen)
    {
        Grid.SetRow(e, fila);
        Grid.SetColumn(e, columna);
        Grid.SetColumnSpan(e, span);
        if (e is FrameworkElement f) f.Margin = margen;
    }

    public void Preferencias(bool minimo, bool tendencias)
    {
        _minimo = minimo;
        _tendencias = tendencias;
    }

    public void Mostrar(string valor, Brush color, string? porcentaje, string detalle)
    {
        Valor.Text = valor;
        Valor.Foreground = color;
        Porcentaje.Text = porcentaje ?? "";
        Porcentaje.Visibility = _minimo || porcentaje is null ? Visibility.Collapsed : Visibility.Visible;
        Detalle.Text = detalle;
        Detalle.Visibility = _minimo || detalle.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public void MostrarHistorial(IReadOnlyCollection<double> historial, double umbral, Brush color, int capacidad)
    {
        Grafico.Visibility = _minimo || !_tendencias || historial.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (Grafico.Visibility != Visibility.Visible) return;

        double ancho = Grafico.Width, alto = Grafico.Height;
        double Y(double v) => alto - (Math.Clamp(v, EjeMin, EjeMax) - EjeMin) / (EjeMax - EjeMin) * alto;

        // Alineados a la derecha: el valor de ahora queda siempre en el borde
        // y la curva «entra» desde la izquierda al arrancar.
        var puntos = new PointCollection(historial.Count);
        double paso = ancho / Math.Max(1, capacidad - 1);
        double x = ancho - paso * (historial.Count - 1);
        foreach (var v in historial)
        {
            puntos.Add(new Point(x, Y(v)));
            x += paso;
        }
        puntos.Freeze();
        Curva.Points = puntos;
        Curva.Stroke = color;
        LineaUmbral.Y1 = LineaUmbral.Y2 = Math.Round(Y(umbral)) + 0.5;
    }
}
