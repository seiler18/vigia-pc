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
        _bucle = Task.Run(async () =>
        {
            // Los contadores de tasa (carga) necesitan dos muestras.
            _consulta.Recolectar();
            using var reloj = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await reloj.WaitForNextTickAsync(_cancelar.Token).ConfigureAwait(false))
                {
                    try { NuevaLectura?.Invoke(Leer()); }
                    catch (Exception e) { Registro.Anotar("Error al leer los sensores", e); }
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private Task? _bucle;

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
            using var proceso = Process.GetProcessById(pid);
            if (NoSonJuegos.Contains(proceso.ProcessName)) return;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return; }
        _pidJuego = pid;
    }

    public void Dispose()
    {
        // Se espera a que termine la lectura en curso antes de cerrar los
        // handles: cerrar la consulta PDH a mitad de una recolección es
        // comportamiento indefinido en código nativo.
        _cancelar.Cancel();
        try { _bucle?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
        _fps.Dispose();
        _gpu.Dispose();
        _consulta.Dispose();
    }
}
