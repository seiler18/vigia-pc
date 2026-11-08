using System.Windows;
using System.Windows.Media;

namespace Vigia.Interfaz;

/// <summary>
/// Tres colores de estado y el resto derivado del texto: así el color solo
/// aparece cuando significa algo. Se recalcula al cambiar la configuración;
/// los XAML lo leen como DynamicResource para cambiar sin reiniciar.
/// </summary>
internal static class Paleta
{
    public static SolidColorBrush Normal { get; private set; } = null!;
    public static SolidColorBrush Atencion { get; private set; } = null!;
    public static SolidColorBrush Critico { get; private set; } = null!;
    public static SolidColorBrush SinDato { get; private set; } = null!;
    public static SolidColorBrush Texto { get; private set; } = null!;
    public static SolidColorBrush Barra { get; private set; } = null!;
    public static SolidColorBrush Separador { get; private set; } = null!;
    public static Color Fondo { get; private set; }

    // El juego para daltonismo evita depender de verde contra rojo (lo que
    // más se confunde): azul, naranja y rosa se distinguen con deuteranopía y
    // protanopía.
    private static readonly (Color Normal, Color Atencion, Color Critico) Semaforo =
        (Color.FromRgb(0x4A, 0xDE, 0x80), Color.FromRgb(0xFB, 0xBF, 0x24), Color.FromRgb(0xF8, 0x71, 0x71));
    private static readonly (Color Normal, Color Atencion, Color Critico) Daltonismo =
        (Color.FromRgb(0x60, 0xA5, 0xFA), Color.FromRgb(0xFB, 0x92, 0x3C), Color.FromRgb(0xF4, 0x72, 0xB6));

    public static void Aplicar(Configuracion c)
    {
        var estados = c.ColoresEstado == "Daltonismo" ? Daltonismo : Semaforo;
        Normal = Pincel(estados.Normal);
        Atencion = Pincel(estados.Atencion);
        Critico = Pincel(estados.Critico);

        var texto = (Color)ColorConverter.ConvertFromString(c.ColorTexto);
        Fondo = (Color)ColorConverter.ConvertFromString(c.ColorFondo);
        Texto = Pincel(texto);
        Barra = Pincel(Color.FromArgb(0x99, texto.R, texto.G, texto.B));
        Separador = Pincel(Color.FromArgb(0x22, texto.R, texto.G, texto.B));
        SinDato = Pincel(Color.FromArgb(0x80, texto.R, texto.G, texto.B));

        var recursos = Application.Current.Resources;
        recursos["TextoPrincipal"] = Texto;
        recursos["TextoSecundario"] = Pincel(Color.FromArgb(0xA6, texto.R, texto.G, texto.B));
        recursos["TextoTenue"] = Pincel(Color.FromArgb(0x73, texto.R, texto.G, texto.B));
        recursos["FondoBarra"] = Pincel(Color.FromArgb(0x33, texto.R, texto.G, texto.B));
        recursos["Separador"] = Separador;
    }

    public static SolidColorBrush De(Nivel nivel) => nivel switch
    {
        Nivel.Critico => Critico,
        Nivel.Atencion => Atencion,
        Nivel.Normal => Normal,
        _ => SinDato,
    };

    /// <summary>El valor grande va en el color del texto si está normal: verde en toda la franja cansa y le quita fuerza al aviso.</summary>
    public static SolidColorBrush ValorDe(Nivel nivel) => nivel is Nivel.Normal or Nivel.SinDato ? Texto : De(nivel);

    private static SolidColorBrush Pincel(Color c)
    {
        var pincel = new SolidColorBrush(c);
        pincel.Freeze();
        return pincel;
    }
}
