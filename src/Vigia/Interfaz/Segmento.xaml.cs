using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Vigia.Interfaz;

public partial class Segmento : UserControl
{
    // Escala fija para que la pendiente signifique lo mismo siempre: con una
    // escala automática, subir de 70 a 71 °C se vería como un salto.
    private const double EjeMin = 40, EjeMax = 100;

    public Segmento() => InitializeComponent();

    public string TituloTexto { set => Titulo.Text = value; }

    public void Mostrar(string valor, Brush color, string? porcentaje, string detalle, bool minimo)
    {
        Valor.Text = valor;
        Valor.Foreground = color;
        Porcentaje.Text = porcentaje ?? "";
        Porcentaje.Visibility = minimo || porcentaje is null ? Visibility.Collapsed : Visibility.Visible;
        Detalle.Text = detalle;
        Detalle.Visibility = minimo || detalle.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public void MostrarHistorial(IReadOnlyCollection<double> historial, double umbral, Brush color, int capacidad, bool minimo)
    {
        Grafico.Visibility = minimo || historial.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
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
