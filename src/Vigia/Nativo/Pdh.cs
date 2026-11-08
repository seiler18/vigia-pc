using System.Runtime.InteropServices;

namespace Vigia.Nativo;

/// <summary>
/// Contadores de rendimiento de Windows. Se agregan siempre por su nombre en
/// inglés con PdhAddEnglishCounter: en un Windows en español los nombres están
/// traducidos («Información del procesador») y la ruta en inglés con
/// PdhAddCounter falla con PDH_CSTATUS_NO_OBJECT.
/// </summary>
internal sealed partial class ConsultaPdh : IDisposable
{
    private nint _consulta;

    public ConsultaPdh()
    {
        if (PdhOpenQueryW(null, 0, out _consulta) != 0)
            throw new InvalidOperationException("No se pudo abrir la consulta de contadores de Windows.");
    }

    /// <summary>Devuelve null si el contador no existe en este equipo.</summary>
    public ContadorPdh? Agregar(string rutaEnIngles) =>
        PdhAddEnglishCounterW(_consulta, rutaEnIngles, 0, out var h) == 0 ? new ContadorPdh(h) : null;

    public void Recolectar() => PdhCollectQueryData(_consulta);

    public void Dispose()
    {
        if (_consulta != 0) PdhCloseQuery(_consulta);
        _consulta = 0;
    }

    [LibraryImport("pdh.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint PdhOpenQueryW(string? fuente, nint usuario, out nint consulta);

    [LibraryImport("pdh.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint PdhAddEnglishCounterW(nint consulta, string ruta, nint usuario, out nint contador);

    [LibraryImport("pdh.dll")]
    private static partial uint PdhCollectQueryData(nint consulta);

    [LibraryImport("pdh.dll")]
    private static partial uint PdhCloseQuery(nint consulta);
}

internal sealed partial class ContadorPdh(nint handle)
{
    private const uint PDH_FMT_DOUBLE = 0x200;
    // Sin esto PDH recorta a 100 los porcentajes, y «% Processor Utility» pasa
    // de 100 con el turbo: el recorte lo hacemos nosotros donde corresponde.
    private const uint PDH_FMT_NOCAP100 = 0x8000;
    private const uint PDH_MORE_DATA = 0x800007D2;

    [StructLayout(LayoutKind.Explicit)]
    private struct ValorFormateado
    {
        [FieldOffset(0)] public uint Estado;
        [FieldOffset(8)] public double Valor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ElementoFormateado
    {
        public nint Nombre;
        public ValorFormateado Valor;
    }

    private static bool Valido(uint estado) => estado is 0 or 1;

    public double? Valor()
    {
        if (PdhGetFormattedCounterValue(handle, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, out _, out var v) != 0) return null;
        return Valido(v.Estado) ? v.Valor : null;
    }

    /// <summary>Para rutas con comodín: un valor por instancia.</summary>
    public unsafe List<(string Instancia, double Valor)> Valores()
    {
        var resultado = new List<(string, double)>();
        uint bytes = 0;
        uint estado = PdhGetFormattedCounterArrayW(handle, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref bytes, out _, 0);
        if (estado != PDH_MORE_DATA || bytes == 0) return resultado;

        var buffer = new byte[bytes];
        fixed (byte* p = buffer)
        {
            if (PdhGetFormattedCounterArrayW(handle, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref bytes, out uint n, (nint)p) != 0)
                return resultado;
            var elementos = (ElementoFormateado*)p;
            for (int i = 0; i < n; i++)
            {
                if (!Valido(elementos[i].Valor.Estado)) continue;
                resultado.Add((Marshal.PtrToStringUni(elementos[i].Nombre) ?? "", elementos[i].Valor.Valor));
            }
        }
        return resultado;
    }

    [LibraryImport("pdh.dll")]
    private static partial uint PdhGetFormattedCounterValue(nint contador, uint formato, out uint tipo, out ValorFormateado valor);

    [LibraryImport("pdh.dll")]
    private static partial uint PdhGetFormattedCounterArrayW(nint contador, uint formato, ref uint bytes, out uint cantidad, nint buffer);
}
