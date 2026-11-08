using Vigia.Nativo;

namespace Vigia.Sensores;

internal static class SensorRam
{
    private const double Gb = 1024d * 1024 * 1024;

    public static LecturaRam Leer()
    {
        var estado = new Sistema.EstadoMemoria { Tamano = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Sistema.EstadoMemoria>() };
        if (!Sistema.GlobalMemoryStatusEx(ref estado)) return new LecturaRam(0, 0);
        return new LecturaRam((estado.FisicaTotal - estado.FisicaDisponible) / Gb, estado.FisicaTotal / Gb);
    }
}
