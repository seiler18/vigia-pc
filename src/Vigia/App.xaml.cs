using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Vigia.Interfaz;
using Vigia.Nativo;
using Vigia.Sensores;

namespace Vigia;

public partial class App : Application
{
    private Mutex? _unica;
    private Recolector? _recolector;
    private Bandeja? _bandeja;
    private FileSystemWatcher? _vigilante;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Un error al pintar una lectura rara no debe cerrar el monitor en
        // plena partida: se anota y la lectura siguiente vuelve a intentarlo.
        DispatcherUnhandledException += (_, a) =>
        {
            Registro.Anotar("Error en la interfaz", a.Exception);
            a.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
        {
            if (a.ExceptionObject is Exception ex) Registro.Anotar("Error fatal", ex);
        };

        // Una sola instancia: con «Iniciar con Windows» activo es fácil abrir
        // una segunda sin darse cuenta, y serían dos franjas y dos sesiones ETW.
        _unica = new Mutex(true, @"Local\Vigia-instancia-unica", out bool esPrimera);
        if (!esPrimera)
        {
            Shutdown();
            return;
        }

        // Lo que hace a Vigía poco invasivo mientras se juega:
        // - dibuja con la CPU, así no le pide nada a la GPU que mueve el juego
        //   (son unas pocas líneas de texto una vez por segundo);
        // - prioridad baja y modo de eficiencia: si el juego necesita CPU, gana.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        using (var yo = Process.GetCurrentProcess()) yo.PriorityClass = ProcessPriorityClass.BelowNormal;
        Sistema.ActivarModoEficiencia();

        bool primerUso = !File.Exists(Configuracion.Ruta);
        var config = Configuracion.Cargar();
        config.Guardar();

        var franja = new Franja(config);
        _bandeja = new Bandeja(config, franja);
        _bandeja.SalirPedido += Shutdown;
        franja.Show();
        if (primerUso) _bandeja.PrimerUso();

        _recolector = new Recolector();
        _recolector.NuevaLectura += lectura => Dispatcher.BeginInvoke(() =>
        {
            var diagnostico = Evaluacion.Evaluar(lectura, config);
            // Aunque esté oculta: así la máxima de la sesión y la tendencia
            // siguen al día. Oculta no se dibuja, así que no cuesta.
            franja.Actualizar(lectura, diagnostico);
            _bandeja?.Actualizar(lectura, diagnostico);
        });
        _recolector.Iniciar();

        VigilarConfiguracion(config);
    }

    /// <summary>
    /// Los umbrales se editan en el JSON con el Bloc de notas; al guardar se
    /// aplican solos, sin reiniciar Vigía.
    /// </summary>
    private void VigilarConfiguracion(Configuracion config)
    {
        _vigilante = new FileSystemWatcher(Configuracion.Carpeta, "config.json")
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _vigilante.Changed += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            Thread.Sleep(100); // el Bloc de notas escribe en dos pasos
            var nueva = Configuracion.Cargar();
            config.TemperaturaGpu = nueva.TemperaturaGpu;
            config.TemperaturaCpu = nueva.TemperaturaCpu;
            config.Ram = nueva.Ram;
            config.AvisarEnCritico = nueva.AvisarEnCritico;
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _vigilante?.Dispose();
        _recolector?.Dispose();
        _bandeja?.Dispose();
        _unica?.Dispose();
        base.OnExit(e);
    }
}
