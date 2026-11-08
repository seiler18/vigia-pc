using System.Diagnostics;
using Vigia.Nativo;

namespace Vigia.Sensores;

/// <summary>
/// Lee todo una vez por segundo en un hilo aparte, para que la consulta de
/// contadores (decenas de milisegundos con muchos procesos) nunca congele la
/// ventana.
/// </summary>
internal sealed class Recolector : IDisposable
{
    // Lo que queda en primer plano al hacer alt-tab o abrir el menú Inicio no
    // es «el juego»: si la ventana activa es una de estas, se sigue mostrando
    // el último juego.
    private static readonly HashSet<string> NoSonJuegos = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost",
        "TextInputHost", "LockApp", "dwm", "Taskmgr", "ShellHost",
    };

    private readonly ConsultaPdh _consulta = new();
    private readonly SensorCpu _cpu;
    private readonly SensorGpu _gpu;
    private readonly SensorFps _fps = new();
    private readonly CancellationTokenSource _cancelar = new();
    private readonly int _pidPropio = Environment.ProcessId;
    private int? _pidJuego;

    public event Action<Lectura>? NuevaLectura;

    public Recolector()
    {
        _cpu = new SensorCpu(_consulta);
        _gpu = new SensorGpu(_consulta);
    }

    public void Iniciar()
    {
        _fps.Iniciar();
        _ = Task.Run(async () =>
        {
            // Los contadores de tasa (carga) necesitan dos muestras.
            _consulta.Recolectar();
            using var reloj = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await reloj.WaitForNextTickAsync(_cancelar.Token).ConfigureAwait(false))
            {
                try { NuevaLectura?.Invoke(Leer()); }
                catch (Exception e) { Debug.WriteLine(e); }
            }
        }, _cancelar.Token);
    }

    public Lectura Leer()
    {
        _consulta.Recolectar();
        ActualizarJuego();

        var (gpu, enIntegrada) = _gpu.Leer(_pidJuego);
        var fps = _pidJuego is int pid ? _fps.Leer(pid) : null;
        return new Lectura(_cpu.Leer(), gpu, SensorRam.Leer(), fps, _fps.Error, fps is not null && enIntegrada);
    }

    private void ActualizarJuego()
    {
        Sistema.GetWindowThreadProcessId(Sistema.GetForegroundWindow(), out int pid);
        if (pid == 0 || pid == _pidPropio) return;
        try
        {
            if (NoSonJuegos.Contains(Process.GetProcessById(pid).ProcessName)) return;
        }
        catch (ArgumentException) { return; }
        _pidJuego = pid;
    }

    public void Dispose()
    {
        _cancelar.Cancel();
        _fps.Dispose();
        _gpu.Dispose();
        _consulta.Dispose();
    }
}
