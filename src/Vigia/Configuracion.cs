using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Vigia;

public sealed class Umbral
{
    public double Atencion { get; set; }
    public double Critico { get; set; }
}

/// <summary>
/// Se guarda en %APPDATA%\Vigia\config.json. Casi todo se cambia desde la
/// ventana «Personalizar» (doble clic en la franja); el archivo se puede editar
/// a mano y se recarga solo al guardarlo.
/// </summary>
public sealed class Configuracion
{
    // Los valores por defecto son de un notebook gamer: la RTX 3050 Laptop
    // empieza a bajarse la frecuencia sola cerca de 87 °C y el Ryzen 4600H
    // tolera hasta 105 °C, pero pasar de 95 °C sostenido ya es mala ventilación.
    public Umbral TemperaturaGpu { get; set; } = new() { Atencion = 80, Critico = 87 };
    public Umbral TemperaturaCpu { get; set; } = new() { Atencion = 85, Critico = 95 };
    public Umbral Ram { get; set; } = new() { Atencion = 85, Critico = 95 };
    public bool AvisarEnCritico { get; set; } = true;

    // --- Posición ---

    /// <summary>"Arriba", "Abajo", "Izquierda" o "Derecha" del monitor elegido.</summary>
    public string Borde { get; set; } = "Arriba";

    /// <summary>
    /// Reservar el espacio como la barra de tareas: las ventanas maximizadas
    /// terminan donde empieza la franja y sus botones de cerrar y minimizar
    /// quedan a la vista.
    /// </summary>
    public bool ReservarEspacio { get; set; } = true;

    // Un punto cualquiera dentro del monitor elegido, en píxeles físicos: se
    // guarda el punto y no el nombre del monitor porque Windows renombra los
    // monitores al reconectarlos; el punto sigue cayendo en la misma pantalla.
    public int? PantallaX { get; set; }
    public int? PantallaY { get; set; }

    public bool Fijado { get; set; }

    // --- Apariencia ---

    /// <summary>Multiplica el grosor de la franja y el tamaño de la letra.</summary>
    public double Escala { get; set; } = 1.0;
    public double Opacidad { get; set; } = 0.9;
    public string ColorFondo { get; set; } = "#14171C";
    public string ColorTexto { get; set; } = "#E8EAED";

    /// <summary>"Semaforo" (verde, ámbar, rojo) o "Daltonismo" (azul, naranja, rosa).</summary>
    public string ColoresEstado { get; set; } = "Semaforo";

    // --- Qué se muestra ---

    public bool MostrarFps { get; set; } = true;
    public bool MostrarGpu { get; set; } = true;
    public bool MostrarCpu { get; set; } = true;
    public bool MostrarRam { get; set; } = true;
    public bool MostrarTendencias { get; set; } = true;

    /// <summary>Solo los valores principales, sin porcentajes ni detalle.</summary>
    public bool Compacto { get; set; }

    private static readonly JsonSerializerOptions Formato = new() { WriteIndented = true };

    public static string Carpeta { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vigia");

    public static string Ruta { get; } = Path.Combine(Carpeta, "config.json");

    public bool EsVertical => Borde is "Izquierda" or "Derecha";

    public static Configuracion Cargar()
    {
        try
        {
            if (File.Exists(Ruta))
                return (JsonSerializer.Deserialize<Configuracion>(File.ReadAllText(Ruta)) ?? new()).Normalizada();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // Un JSON roto a mano no debe impedir que Vigía arranque: se usan
            // los valores por defecto y el archivo se reescribe al guardar.
        }
        return new();
    }

    /// <summary>
    /// El archivo se puede editar a mano: un umbral en null o una opacidad de 5
    /// no pueden tumbar a Vigía (se evalúa cada segundo; un null ahí sería una
    /// caída por segundo). Lo inválido vuelve al valor por defecto.
    /// </summary>
    public Configuracion Normalizada()
    {
        var porDefecto = new Configuracion();
        TemperaturaGpu = Valido(TemperaturaGpu, 30, 120) ? TemperaturaGpu : porDefecto.TemperaturaGpu;
        TemperaturaCpu = Valido(TemperaturaCpu, 30, 120) ? TemperaturaCpu : porDefecto.TemperaturaCpu;
        Ram = Valido(Ram, 10, 100) ? Ram : porDefecto.Ram;
        Opacidad = double.IsFinite(Opacidad) ? Math.Clamp(Opacidad, 0.3, 1) : porDefecto.Opacidad;
        Escala = double.IsFinite(Escala) ? Math.Clamp(Escala, 0.8, 1.6) : porDefecto.Escala;
        if (Borde is not ("Arriba" or "Abajo" or "Izquierda" or "Derecha")) Borde = porDefecto.Borde;
        if (ColoresEstado is not ("Semaforo" or "Daltonismo")) ColoresEstado = porDefecto.ColoresEstado;
        if (!EsColor(ColorFondo)) ColorFondo = porDefecto.ColorFondo;
        if (!EsColor(ColorTexto)) ColorTexto = porDefecto.ColorTexto;
        // Todo apagado dejaría una franja vacía sin forma de saber por qué.
        if (!MostrarFps && !MostrarGpu && !MostrarCpu && !MostrarRam) MostrarGpu = MostrarCpu = true;
        return this;
    }

    private static bool Valido(Umbral? u, double min, double max) =>
        u is not null && u.Atencion >= min && u.Critico <= max && u.Atencion <= u.Critico;

    /// <summary>Solo «#RRGGBB»: lo que el selector de colores escribe y lo único que se valida fácil a mano.</summary>
    public static bool EsColor(string? texto) =>
        texto is { Length: 7 } && texto[0] == '#'
        && int.TryParse(texto.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);

    /// <summary>
    /// Vuelve la apariencia y lo que se muestra a como venían, sin tocar dónde
    /// está la franja ni los umbrales.
    /// </summary>
    public void RestablecerApariencia()
    {
        var d = new Configuracion();
        (Escala, Opacidad, ColorFondo, ColorTexto, ColoresEstado) = (d.Escala, d.Opacidad, d.ColorFondo, d.ColorTexto, d.ColoresEstado);
        (MostrarFps, MostrarGpu, MostrarCpu, MostrarRam, MostrarTendencias, Compacto) =
            (d.MostrarFps, d.MostrarGpu, d.MostrarCpu, d.MostrarRam, d.MostrarTendencias, d.Compacto);
    }

    /// <summary>
    /// Copia todo sobre esta misma instancia: la franja, la bandeja y la
    /// evaluación guardan una referencia a ella y deben ver el cambio sin
    /// que nadie les pase una nueva.
    /// </summary>
    public void CopiarDe(Configuracion otra)
    {
        foreach (var p in typeof(Configuracion).GetProperties())
            if (p.CanWrite) p.SetValue(this, p.GetValue(otra));
    }

    /// <summary>No lanza: no poder guardar la posición no es motivo para cerrar el monitor en plena partida.</summary>
    public void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Carpeta);
            File.WriteAllText(Ruta, JsonSerializer.Serialize(this, Formato));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Anotar("No se pudo guardar la configuración", e);
        }
    }
}
