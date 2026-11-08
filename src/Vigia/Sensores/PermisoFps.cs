using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace Vigia.Sensores;

/// <summary>
/// Los FPS salen de una sesión ETW, y Windows solo deja abrirla a un
/// administrador o a quien esté en el grupo «Usuarios del registro de
/// rendimiento» (S-1-5-32-559). Vigía no pide administrador para funcionar:
/// en vez de eso, ofrece agregar al usuario a ese grupo una sola vez.
/// </summary>
internal static class PermisoFps
{
    private const string SidGrupo = "S-1-5-32-559";

    /// <summary>
    /// Si el usuario ya puede leer FPS. Mira el token de la sesión, no la
    /// lista de miembros: recién agregado al grupo, el permiso no vale hasta
    /// cerrar sesión y volver a entrar, y eso es lo que importa aquí.
    /// </summary>
    public static bool Tiene()
    {
        using var yo = WindowsIdentity.GetCurrent();
        if (new WindowsPrincipal(yo).IsInRole(WindowsBuiltInRole.Administrator)) return true;
        return yo.Groups?.Any(g => g.Value == SidGrupo) == true;
    }

    public enum Resultado { Listo, Cancelado, Fallo }

    /// <summary>
    /// Agrega al usuario al grupo con una PowerShell elevada: Windows muestra
    /// su confirmación (UAC) y nada más corre como administrador. Al usuario
    /// se lo nombra por su SID y no por su nombre: si quien confirma el UAC es
    /// otra cuenta de administrador, el permiso igual queda para quien usa
    /// Vigía, y el nombre del grupo cambia con el idioma de Windows, el SID no.
    /// </summary>
    public static async Task<Resultado> ConcederAsync()
    {
        string? usuario;
        using (var yo = WindowsIdentity.GetCurrent()) usuario = yo.User?.Value;
        if (usuario is null) return Resultado.Fallo;

        // Ya ser miembro (agregado antes, sin haber cerrado sesión) cuenta
        // como éxito: lo que falta es lo mismo, volver a entrar. Se reconoce
        // por el id del error y no con «catch [MemberExistsException]»: ese
        // tipo no existe hasta que PowerShell carga el módulo LocalAccounts, y
        // un catch con un tipo desconocido rompe el script entero.
        string script =
            $"try {{ Add-LocalGroupMember -SID {SidGrupo} -Member '{usuario}' -ErrorAction Stop; exit 0 }} " +
            "catch { if ($_.FullyQualifiedErrorId -like 'MemberExists*') { exit 0 } else { exit 1 } }";

        // Codificado en base64 (UTF-16) y no como texto: así ninguna comilla
        // ni llave del script depende de cómo Windows parte los argumentos.
        string codificado = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var inicio = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -EncodedCommand {codificado}")
        {
            Verb = "runas",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var proceso = Process.Start(inicio);
            if (proceso is null) return Resultado.Fallo;
            await proceso.WaitForExitAsync();
            return proceso.ExitCode == 0 ? Resultado.Listo : Resultado.Fallo;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            return Resultado.Cancelado; // ERROR_CANCELLED: dijo «No» en el UAC
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            Registro.Anotar("No se pudo conceder el permiso de FPS", e);
            return Resultado.Fallo;
        }
    }
}
