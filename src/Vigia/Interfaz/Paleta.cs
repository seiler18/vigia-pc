using System.Windows.Media;

namespace Vigia.Interfaz;

/// <summary>
/// Tres colores de estado y nada más: verde, ámbar y rojo se distinguen de
/// reojo sobre cualquier juego. El resto del panel es gris para que el color
/// solo aparezca cuando significa algo.
/// </summary>
internal static class Paleta
{
    public static readonly SolidColorBrush Normal = Congelar(0x4A, 0xDE, 0x80);
    public static readonly SolidColorBrush Atencion = Congelar(0xFB, 0xBF, 0x24);
    public static readonly SolidColorBrush Critico = Congelar(0xF8, 0x71, 0x71);
    public static readonly SolidColorBrush SinDato = Congelar(0x6B, 0x72, 0x80);
    public static readonly SolidColorBrush Texto = Congelar(0xE8, 0xEA, 0xED);
    public static readonly SolidColorBrush Barra = Congelar(0x9A, 0xA0, 0xA6);

    public static SolidColorBrush De(Nivel nivel) => nivel switch
    {
        Nivel.Critico => Critico,
        Nivel.Atencion => Atencion,
        Nivel.Normal => Normal,
        _ => SinDato,
    };

    /// <summary>El valor grande va en blanco si está normal: verde en todo el panel cansa y le quita fuerza al aviso.</summary>
    public static SolidColorBrush ValorDe(Nivel nivel) => nivel is Nivel.Normal or Nivel.SinDato ? Texto : De(nivel);

    private static SolidColorBrush Congelar(byte r, byte g, byte b)
    {
        var pincel = new SolidColorBrush(Color.FromRgb(r, g, b));
        pincel.Freeze();
        return pincel;
    }
}
