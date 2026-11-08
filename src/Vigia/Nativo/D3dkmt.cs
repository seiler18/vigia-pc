using System.Runtime.InteropServices;

namespace Vigia.Nativo;

/// <summary>
/// La interfaz del kernel gráfico de Windows (WDDM). Es la que usa el
/// Administrador de tareas para mostrar la temperatura de la GPU: no necesita
/// driver propio ni administrador, y funciona igual con NVIDIA, AMD e Intel.
/// Los números de KMTQAITYPE salen de d3dkmthk.h del Windows SDK.
/// </summary>
internal static partial class D3dkmt
{
    public const int KMTQAITYPE_GETSEGMENTSIZE = 3;
    public const int KMTQAITYPE_ADAPTERREGISTRYINFO = 8;
    public const int KMTQAITYPE_ADAPTERTYPE = 15;
    public const int KMTQAITYPE_ADAPTERPERFDATA = 62;
    public const int KMTQAITYPE_ADAPTERPERFDATA_CAPS = 63;

    [StructLayout(LayoutKind.Sequential)]
    public struct Luid
    {
        public uint Bajo;
        public int Alto;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InfoAdaptador
    {
        public uint Handle;
        public Luid Luid;
        public uint NumFuentes;
        public int PrefiereRegionesPrecisas;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EnumAdaptadores2
    {
        public uint Cantidad;
        public nint Adaptadores;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ConsultaInfo
    {
        public uint Adaptador;
        public int Tipo;
        public nint Datos;
        public uint TamanoDatos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Segmentos
    {
        public ulong MemoriaDedicada;
        public ulong MemoriaSistemaDedicada;
        public ulong MemoriaSistemaCompartida;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct Registro
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Nombre;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Bios;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Dac;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Chip;
    }

    /// <summary>Temperatura en décimas de °C; 0 si la GPU no la informa.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Rendimiento
    {
        public uint IndiceFisico;
        public ulong FrecuenciaMemoria;
        public ulong FrecuenciaMemoriaMax;
        public ulong FrecuenciaMemoriaMaxOc;
        public ulong AnchoBandaMemoria;
        public ulong AnchoBandaPcie;
        public uint VentiladorRpm;
        public uint Potencia;
        public uint Temperatura;
        public byte EstadoEnergia;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CapacidadesRendimiento
    {
        public uint IndiceFisico;
        public ulong AnchoBandaMemoriaMax;
        public ulong AnchoBandaPcieMax;
        public uint VentiladorRpmMax;
        public uint TemperaturaMax;
        public uint TemperaturaAviso;
    }

    [Flags]
    public enum TipoAdaptador : uint
    {
        Render = 1 << 0,
        Pantalla = 1 << 1,
        Software = 1 << 2,
        HibridaDiscreta = 1 << 4,
        HibridaIntegrada = 1 << 5,
    }

    public static unsafe bool Consultar<T>(uint adaptador, int tipo, ref T datos) where T : unmanaged
    {
        fixed (T* p = &datos)
        {
            var c = new ConsultaInfo { Adaptador = adaptador, Tipo = tipo, Datos = (nint)p, TamanoDatos = (uint)sizeof(T) };
            return D3DKMTQueryAdapterInfo(ref c) == 0;
        }
    }

    public static unsafe string? Nombre(uint adaptador)
    {
        int tamano = Marshal.SizeOf<Registro>();
        nint p = Marshal.AllocHGlobal(tamano);
        try
        {
            new Span<byte>((void*)p, tamano).Clear();
            var c = new ConsultaInfo { Adaptador = adaptador, Tipo = KMTQAITYPE_ADAPTERREGISTRYINFO, Datos = p, TamanoDatos = (uint)tamano };
            if (D3DKMTQueryAdapterInfo(ref c) != 0) return null;
            return Marshal.PtrToStructure<Registro>(p).Nombre;
        }
        finally { Marshal.FreeHGlobal(p); }
    }

    public static unsafe List<InfoAdaptador> Enumerar()
    {
        var e = new EnumAdaptadores2();
        if (D3DKMTEnumAdapters2(ref e) != 0 || e.Cantidad == 0) return [];
        var lista = new InfoAdaptador[e.Cantidad];
        fixed (InfoAdaptador* p = lista)
        {
            e.Adaptadores = (nint)p;
            if (D3DKMTEnumAdapters2(ref e) != 0) return [];
        }
        return lista.Take((int)e.Cantidad).ToList();
    }

    public static void Cerrar(uint adaptador)
    {
        var c = adaptador;
        D3DKMTCloseAdapter(ref c);
    }

    [LibraryImport("gdi32.dll")]
    private static partial int D3DKMTEnumAdapters2(ref EnumAdaptadores2 e);

    [LibraryImport("gdi32.dll")]
    private static partial int D3DKMTQueryAdapterInfo(ref ConsultaInfo c);

    [LibraryImport("gdi32.dll")]
    private static partial int D3DKMTCloseAdapter(ref uint adaptador);
}
