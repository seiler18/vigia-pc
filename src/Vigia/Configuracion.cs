using System.IO;
using System.Text.Json;

namespace Vigia;

public sealed class Umbral
{
    public double Atencion { get; set; }
    public double Critico { get; set; }
}

/// <summary>
/// Se guarda en %APPDATA%\Vigia\config.json. Los umbrales se editan a mano ahí
/// (menú de la bandeja → «Abrir configuración»); lo demás lo guarda la propia
/// ventana al moverla o cambiar de modo.
/// </summary>
public sealed class Configuracion
{
    // Los valores por defecto son de un notebook gamer: la RTX 3050 Laptop
    // empieza a bajarse la frecuencia sola cerca de 87 °C y el Ryzen 4600H
    // tolera hasta 105 °C, pero pasar de 95 °C sostenido ya es mala ventilación.
    public Umbral TemperaturaGpu { get; set; } = new() { Atencion = 80, Critico = 87 };
    public Umbral TemperaturaCpu { get; set; } = new() { Atencion = 85, Critico = 95 };
    public Umbral Ram { get; set; } = new() { Atencion = 85, Critico = 95 };

    public bool Compacto { get; set; }
    public bool Fijado { get; set; }
    public bool AvisarEnCritico { get; set; } = true;
    public double Opacidad { get; set; } = 0.9;

    /// <summary>"Arriba" o "Abajo" del monitor elegido.</summary>
    public string Borde { get; set; } = "Arriba";

    // Un punto cualquiera dentro del monitor elegido, en píxeles físicos: se
    // guarda el punto y no el nombre del monitor porque Windows renombra los
    // monitores al reconectarlos; el punto sigue cayendo en la misma pantalla.
    public int? PantallaX { get; set; }
    public int? PantallaY { get; set; }

    private static readonly JsonSerializerOptions Formato = new() { WriteIndented = true };

    public static string Carpeta { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vigia");

    public static string Ruta { get; } = Path.Combine(Carpeta, "config.json");

    public static Configuracion Cargar()
    {
        try
        {
            if (File.Exists(Ruta))
                return JsonSerializer.Deserialize<Configuracion>(File.ReadAllText(Ruta)) ?? new();
        }
        catch (JsonException)
        {
            // Un JSON roto a mano no debe impedir que Vigía arranque: se usan
            // los valores por defecto y el archivo se reescribe al guardar.
        }
        return new();
    }

    public void Guardar()
    {
        Directory.CreateDirectory(Carpeta);
        File.WriteAllText(Ruta, JsonSerializer.Serialize(this, Formato));
    }
}
