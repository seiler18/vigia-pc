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
    private Franja? _franja;
    private Ajustes? _ajustes;
    private Configuracion _config = new();
    private string _coloresEstado = "";

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
            // Si no, el espacio reservado quedaría como un hueco vacío en el
            // borde de la pantalla hasta reiniciar el Explorador.
            _franja?.QuitarReserva();
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
        var config = _config = Configuracion.Cargar();
        config.Guardar();
        Paleta.Aplicar(config);
        _coloresEstado = config.ColoresEstado;

        var franja = _franja = new Franja(config);
        _bandeja = new Bandeja(config, franja);
        _bandeja.SalirPedido += Shutdown;
        _bandeja.PersonalizarPedido += AbrirAjustes;
        franja.PersonalizarPedido += AbrirAjustes;
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

        // Para un acceso directo que abra Vigía directo en «Personalizar».
        if (e.Args.Contains("--personalizar", StringComparer.OrdinalIgnoreCase)) AbrirAjustes();
    }

    private void AbrirAjustes()
    {
        if (_ajustes is not null)
        {
            _ajustes.Activate();
            return;
        }
        _ajustes = new Ajustes(_config);
        _ajustes.Cambio += AplicarConfiguracion;
        _ajustes.Closed += (_, _) => _ajustes = null;
        _ajustes.Show();
    }

    /// <summary>Lleva un cambio de configuración a la franja y al icono, sin reiniciar.</summary>
    private void AplicarConfiguracion()
    {
        Paleta.Aplicar(_config);
        _franja?.AplicarConfiguracion();
        if (_config.ColoresEstado != _coloresEstado)
        {
            _coloresEstado = _config.ColoresEstado;
            _bandeja?.RefrescarIconos();
        }
    }

    /// <summary>
    /// El JSON también se puede editar a mano: al guardarlo se aplica solo.
    /// Vigía mismo lo reescribe al mover la franja o cerrar «Personalizar»;
    /// esa recarga trae los mismos valores y no cambia nada.
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
            // Con «Personalizar» abierto manda lo que se ve ahí: recargar el
            // archivo desharía lo que se está ajustando y aún no se guarda.
            if (_ajustes is not null) return;
            Thread.Sleep(100); // el Bloc de notas escribe en dos pasos
            config.CopiarDe(Configuracion.Cargar());
            AplicarConfiguracion();
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
