using Vigia.Sensores;

namespace Vigia;

public enum Nivel { SinDato, Normal, Atencion, Critico }

public sealed record Diagnostico(Nivel Nivel, string Mensaje, Nivel Cpu, Nivel Gpu, Nivel Ram);

/// <summary>
/// Convierte las lecturas en una sola frase arriba del panel: lo que hay que
/// mirar primero. Con una mirada de reojo en plena partida no se alcanzan a
/// leer cuatro números; un color y tres palabras, sí.
/// </summary>
public static class Evaluacion
{
    public static Nivel Clasificar(double? valor, Umbral u) => valor switch
    {
        null => Nivel.SinDato,
        var v when v >= u.Critico => Nivel.Critico,
        var v when v >= u.Atencion => Nivel.Atencion,
        _ => Nivel.Normal,
    };

    public static Diagnostico Evaluar(Lectura l, Configuracion c)
    {
        var cpu = Clasificar(l.Cpu.Temperatura, c.TemperaturaCpu);
        var gpu = Clasificar(l.Gpu?.Temperatura, c.TemperaturaGpu);
        var ram = Clasificar(l.Ram.Porcentaje, c.Ram);

        var peor = new[] { gpu, cpu, ram }.Max();
        if (peor >= Nivel.Atencion)
        {
            // Las temperaturas se nombran juntas («GPU y CPU calientes»): el
            // número ya está al lado en la franja, la frase solo dice qué mirar.
            var calientes = new[] { ("GPU", gpu), ("CPU", cpu) }.Where(c => c.Item2 >= Nivel.Atencion).ToList();
            bool muy = calientes.Any(c => c.Item2 == Nivel.Critico);
            var partes = new List<string>();
            if (calientes.Count == 2) partes.Add($"GPU y CPU {(muy ? "muy calientes" : "calientes")}");
            else if (calientes.Count == 1) partes.Add($"{calientes[0].Item1} {(muy ? "muy caliente" : "caliente")}");
            if (ram >= Nivel.Atencion) partes.Add(ram == Nivel.Critico ? "RAM llena" : "RAM casi llena");
            return new Diagnostico(peor, string.Join(" · ", partes), cpu, gpu, ram);
        }

        if (l.JuegoEnIntegrada)
            return new Diagnostico(Nivel.Atencion, "El juego usa la gráfica integrada", cpu, gpu, ram);

        return new Diagnostico(Nivel.Normal, "Todo en orden", cpu, gpu, ram);
    }
}
