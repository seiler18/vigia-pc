using System.Runtime.InteropServices;

namespace Vigia.Nativo;

/// <summary>
/// Registra la franja como «barra de aplicación» de Windows, el mismo
/// mecanismo de la barra de tareas: Windows le reserva su franja de pantalla
/// y las ventanas maximizadas terminan donde empieza Vigía, en vez de quedar
/// con los botones de minimizar y cerrar tapados debajo.
///
/// Un juego en pantalla completa sin bordes ignora el área reservada (ocupa
/// todo el monitor), así que encima de un juego la franja se sigue viendo.
/// </summary>
internal static partial class BarraDeAplicacion
{
    public enum Borde : uint { Izquierda = 0, Arriba = 1, Derecha = 2, Abajo = 3 }

    // No se usa ABM_WINDOWPOSCHANGED: es para barras que se ocultan solas, y
    // mandarlo en cada movimiento puede rebotar como ABN_POSCHANGED.
    private const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3;
    public const int ABN_POSCHANGED = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct Datos
    {
        public uint Tamano;
        public nint Ventana;
        public uint MensajeAviso;
        public uint Borde;
        public Sistema.Rect Rect;
        public nint Parametro;
    }

    [LibraryImport("shell32.dll")]
    private static partial nuint SHAppBarMessage(uint mensaje, ref Datos datos);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string nombre);

    /// <summary>El mensaje con que Windows avisa que hay que reacomodarse (otra barra cambió, se movió la barra de tareas).</summary>
    public static uint MensajeAviso { get; } = RegisterWindowMessage("Vigia.BarraDeAplicacion");

    private static Datos Base(nint ventana) => new()
    {
        Tamano = (uint)Marshal.SizeOf<Datos>(),
        Ventana = ventana,
        MensajeAviso = MensajeAviso,
    };

    public static void Registrar(nint ventana)
    {
        var d = Base(ventana);
        SHAppBarMessage(ABM_NEW, ref d);
    }

    public static void Quitar(nint ventana)
    {
        var d = Base(ventana);
        SHAppBarMessage(ABM_REMOVE, ref d);
    }

    /// <summary>
    /// Pide a Windows la franja del borde en ese monitor y la reserva. Devuelve
    /// el rectángulo final: Windows lo corre si ya hay otra barra en ese borde
    /// (la barra de tareas, por ejemplo), y la franja queda al lado, no encima.
    /// </summary>
    public static Sistema.Rect Reservar(nint ventana, Borde borde, Sistema.Rect monitor, int grosor)
    {
        var d = Base(ventana);
        d.Borde = (uint)borde;
        d.Rect = monitor;
        Recortar(ref d.Rect, borde, grosor);

        SHAppBarMessage(ABM_QUERYPOS, ref d);
        // QUERYPOS puede mover el lado de afuera y también el de adentro: se
        // vuelve a fijar el grosor desde el lado que Windows dejó.
        Recortar(ref d.Rect, borde, grosor);
        SHAppBarMessage(ABM_SETPOS, ref d);
        return d.Rect;
    }

    public static void Recortar(ref Sistema.Rect r, Borde borde, int grosor)
    {
        switch (borde)
        {
            case Borde.Arriba: r.Abajo = r.Arriba + grosor; break;
            case Borde.Abajo: r.Arriba = r.Abajo - grosor; break;
            case Borde.Izquierda: r.Derecha = r.Izquierda + grosor; break;
            case Borde.Derecha: r.Izquierda = r.Derecha - grosor; break;
        }
    }
}
