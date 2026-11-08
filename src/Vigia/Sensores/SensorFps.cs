using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace Vigia.Sensores;

/// <summary>
/// Cuenta los cuadros escuchando los eventos que Windows ya emite cada vez que
/// un programa presenta un cuadro (ETW, proveedores DXGI y D3D9). Es el método
/// de PresentMon: no se inyecta nada en el juego, así que no lo ve ningún
/// anti-cheat y no puede botarlo. Otros overlays (RTSS) se enganchan dentro del
/// proceso del juego; eso es justo lo que aquí se evita.
///
/// Requiere ser administrador o estar en el grupo «Usuarios del registro de
/// rendimiento». No cubre juegos Vulkan/OpenGL que no presenten por DXGI (con
/// NVIDIA, la opción «Vulkan/OpenGL present method» en automático sí lo hace).
/// </summary>
internal sealed class SensorFps : IDisposable
{
    private static readonly Guid Dxgi = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private static readonly Guid D3d9 = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");
    private const int DxgiPresentStart = 42;
    private const int D3d9PresentStart = 1;
    private const string NombreSesion = "Vigia-FPS";

    // 10 s de historia por proceso: lo justo para un «1 % bajo» con sentido.
    private const double VentanaMs = 10_000;

    private sealed class Historia
    {
        public readonly Queue<double> Marcas = new();
        public long UltimaLlegada;
    }

    private readonly Lock _cerrojo = new();
    private readonly Dictionary<int, Historia> _porProceso = [];
    private readonly Dictionary<int, string> _nombres = [];
    private TraceEventSession? _sesion;

    public string? Error { get; private set; }

    public void Iniciar()
    {
        try
        {
            // Si una sesión anterior quedó viva (Vigía se cerró de golpe),
            // crearla con el mismo nombre la reemplaza en vez de fallar.
            _sesion = new TraceEventSession(NombreSesion) { StopOnDispose = true };
            _sesion.EnableProvider(Dxgi, TraceEventLevel.Informational);
            _sesion.EnableProvider(D3d9, TraceEventLevel.Informational);
            _sesion.Source.AllEvents += AlPresentar;

            var hilo = new Thread(() =>
            {
                try { _sesion.Source.Process(); }
                catch (Exception e) { Error = e.Message; }
            })
            { IsBackground = true, Name = "Vigia ETW", Priority = ThreadPriority.BelowNormal };
            hilo.Start();
        }
        catch (UnauthorizedAccessException)
        {
            // Corto porque va en la franja; el doble clic abre «Personalizar»,
            // donde está el botón que da el permiso (ver PermisoFps).
            Error = "sin permiso · doble clic para activarlo";
        }
        catch (Exception e)
        {
            Error = e.Message;
        }
    }

    private void AlPresentar(TraceEvent e)
    {
        int id = (int)e.ID;
        if (!((id == DxgiPresentStart && e.ProviderGuid == Dxgi) || (id == D3d9PresentStart && e.ProviderGuid == D3d9)))
            return;

        lock (_cerrojo)
        {
            if (!_porProceso.TryGetValue(e.ProcessID, out var h))
                _porProceso[e.ProcessID] = h = new Historia();
            h.Marcas.Enqueue(e.TimeStampRelativeMSec);
            h.UltimaLlegada = Stopwatch.GetTimestamp();
            while (h.Marcas.Count > 0 && e.TimeStampRelativeMSec - h.Marcas.Peek() > VentanaMs)
                h.Marcas.Dequeue();
        }
    }

    public LecturaFps? Leer(int pid)
    {
        double[] marcas;
        lock (_cerrojo)
        {
            Limpiar();
            if (!_porProceso.TryGetValue(pid, out var h)) return null;
            // Los eventos llegan en tandas de hasta ~1 s; si pasaron 3 s sin
            // nada, el proceso dejó de dibujar (pausa, minimizado).
            if (Stopwatch.GetElapsedTime(h.UltimaLlegada).TotalSeconds > 3) return null;
            marcas = [.. h.Marcas];
        }
        if (marcas.Length < 3) return null;

        double ultima = marcas[^1];
        int enElUltimoSegundo = marcas.Count(m => ultima - m < 1000);
        double fps = enElUltimoSegundo;

        // 1 % bajo: los FPS que corresponden al 1 % de cuadros más lentos. Es
        // lo que se siente como tirón aunque el promedio se vea bien.
        double? bajo = null;
        if (marcas.Length > 100)
        {
            var intervalos = new double[marcas.Length - 1];
            for (int i = 1; i < marcas.Length; i++) intervalos[i - 1] = marcas[i] - marcas[i - 1];
            Array.Sort(intervalos);
            double p99 = intervalos[(int)(intervalos.Length * 0.99)];
            if (p99 > 0) bajo = 1000 / p99;
        }

        return new LecturaFps(NombreDe(pid), fps, bajo);
    }

    private void Limpiar()
    {
        foreach (var (pid, h) in _porProceso.ToList())
            if (Stopwatch.GetElapsedTime(h.UltimaLlegada).TotalSeconds > 30)
            {
                _porProceso.Remove(pid);
                _nombres.Remove(pid);
            }
    }

    private string NombreDe(int pid)
    {
        lock (_cerrojo)
        {
            if (_nombres.TryGetValue(pid, out var n)) return n;
        }
        string nombre;
        try
        {
            using var proceso = Process.GetProcessById(pid);
            nombre = proceso.ProcessName;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { nombre = $"proceso {pid}"; }
        lock (_cerrojo) _nombres[pid] = nombre;
        return nombre;
    }

    public void Dispose()
    {
        _sesion?.Dispose();
        _sesion = null;
    }
}
