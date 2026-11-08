using System.Runtime.InteropServices;

namespace Vigia.Nativo;

internal static partial class Sistema
{
    [StructLayout(LayoutKind.Sequential)]
    public struct EstadoMemoria
    {
        public uint Tamano;
        public uint CargaPorcentaje;
        public ulong FisicaTotal;
        public ulong FisicaDisponible;
        public ulong PaginacionTotal;
        public ulong PaginacionDisponible;
        public ulong VirtualTotal;
        public ulong VirtualDisponible;
        public ulong VirtualExtendidaDisponible;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalMemoryStatusEx(ref EstadoMemoria estado);

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint ventana, out int pid);

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_EX_TOOLWINDOW = 0x80;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint ventana, int indice);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(nint ventana, int indice, nint valor);

    private static readonly nint HWND_TOPMOST = -1;
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint ventana, nint despuesDe, int x, int y, int ancho, int alto, uint banderas);

    public static void TraerAlFrente(nint ventana)
    {
        if (ventana != 0) SetWindowPos(ventana, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>En píxeles físicos: con pantallas de distinta escala, las unidades de WPF no sirven para alinear al borde.</summary>
    public static void Colocar(nint ventana, Rect r) =>
        SetWindowPos(ventana, HWND_TOPMOST, r.Izquierda, r.Arriba, r.Derecha - r.Izquierda, r.Abajo - r.Arriba, SWP_NOACTIVATE);

    [StructLayout(LayoutKind.Sequential)]
    public struct Punto
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Izquierda, Arriba, Derecha, Abajo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InfoMonitor
    {
        public uint Tamano;
        public Rect Monitor;
        public Rect Trabajo;
        public uint Banderas;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out Punto p);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(Punto p, uint banderas);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitor, ref InfoMonitor info);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int tipo, out uint dpiX, out uint dpiY);

    /// <summary>
    /// El área de trabajo (sin la barra de tareas) y la escala del monitor que
    /// contiene el punto. Si ese monitor ya no está conectado, el principal.
    /// </summary>
    public static (Rect Trabajo, double Escala) MonitorEn(Punto p)
    {
        const uint MONITOR_DEFAULTTOPRIMARY = 1;
        nint monitor = MonitorFromPoint(p, MONITOR_DEFAULTTOPRIMARY);
        var info = new InfoMonitor { Tamano = (uint)Marshal.SizeOf<InfoMonitor>() };
        GetMonitorInfo(monitor, ref info);
        double escala = GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 ? dpi / 96d : 1;
        return (info.Trabajo, escala);
    }

    public delegate void ProcesarEventoVentana(nint gancho, uint evento, nint ventana, int objeto, int hijo, uint hilo, uint tiempo);

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

    // DllImport y no LibraryImport: el generador de LibraryImport no sabe
    // pasar delegados a código nativo.
    [DllImport("user32.dll")]
    public static extern nint SetWinEventHook(uint minimo, uint maximo, nint modulo, ProcesarEventoVentana funcion, uint proceso, uint hilo, uint banderas);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWinEvent(nint gancho);

    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_CONTROL = 0x2;
    public const uint MOD_SHIFT = 0x4;
    public const uint MOD_NOREPEAT = 0x4000;
    public const uint VK_F11 = 0x7A;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint ventana, int id, uint modificadores, uint tecla);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint ventana, int id);

    [StructLayout(LayoutKind.Sequential)]
    private struct EstadoAhorro
    {
        public uint Version;
        public uint Mascara;
        public uint Estado;
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessInformation(nint proceso, int clase, ref EstadoAhorro info, uint tamano);

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    /// <summary>
    /// El «modo de eficiencia» del Administrador de tareas (EcoQoS): Windows
    /// manda el proceso a los núcleos de bajo consumo y no le sube la
    /// frecuencia. A Vigía le sobra con eso para leer una vez por segundo, y
    /// así no le quita CPU ni temperatura al juego.
    /// </summary>
    public static void ActivarModoEficiencia()
    {
        const int ProcessPowerThrottling = 4;
        const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
        var estado = new EstadoAhorro
        {
            Version = 1,
            Mascara = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            Estado = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
        };
        SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref estado, (uint)Marshal.SizeOf<EstadoAhorro>());
    }
}
