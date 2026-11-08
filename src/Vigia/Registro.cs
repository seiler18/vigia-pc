using System.IO;

namespace Vigia;

/// <summary>
/// Errores a %APPDATA%\Vigia\errores.log. Vigía corre sin ventana de consola:
/// sin esto, un fallo solo se notaría como una franja que desapareció.
/// El archivo se recorta a 256 KB para que no crezca sin fin en un equipo
/// que lo deja abierto meses.
/// </summary>
internal static class Registro
{
    private const long TamanoMaximo = 256 * 1024;
    private static readonly Lock Cerrojo = new();

    public static string Ruta { get; } = Path.Combine(Configuracion.Carpeta, "errores.log");

    public static void Anotar(string contexto, Exception e)
    {
        try
        {
            lock (Cerrojo)
            {
                Directory.CreateDirectory(Configuracion.Carpeta);
                if (File.Exists(Ruta) && new FileInfo(Ruta).Length > TamanoMaximo) File.Delete(Ruta);
                File.AppendAllText(Ruta, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} · {contexto}{Environment.NewLine}{e}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // Si ni siquiera se puede escribir el registro, no hay a quién avisar.
        }
    }
}
