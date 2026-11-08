using Vigia.Nativo;

namespace Vigia.Sensores;

/// <summary>
/// La temperatura sale de la zona térmica ACPI y no del sensor interno del
/// Ryzen: leer el sensor interno exige un driver de kernel (el de
/// LibreHardwareMonitor) y ejecutar como administrador, justo lo que Vigía
/// evita. En los notebooks la zona ACPI la alimenta el firmware con el sensor
/// del CPU, así que sigue de cerca al valor real, aunque se actualiza más lento.
/// </summary>
internal sealed class SensorCpu(ConsultaPdh consulta)
{
    private readonly ContadorPdh? _utilidad = consulta.Agregar(@"\Processor Information(_Total)\% Processor Utility");
    private readonly ContadorPdh? _rendimiento = consulta.Agregar(@"\Processor Information(_Total)\% Processor Performance");
    private readonly ContadorPdh? _frecuenciaBase = consulta.Agregar(@"\Processor Information(_Total)\Processor Frequency");
    private readonly ContadorPdh? _zonas = consulta.Agregar(@"\Thermal Zone Information(*)\High Precision Temperature");

    public LecturaCpu Leer()
    {
        // «% Processor Utility» es la que muestra el Administrador de tareas;
        // pasa de 100 con el turbo, y aquí lo que interesa es qué tan ocupado está.
        double? carga = _utilidad?.Valor() is double u ? Math.Clamp(u, 0, 100) : null;

        double? mhz = _frecuenciaBase?.Valor() is double b && _rendimiento?.Valor() is double r ? b * r / 100 : null;

        // Décimas de kelvin. Con varias zonas se toma la más caliente; las que
        // marcan bajo 10 °C son zonas sin sensor real detrás.
        double? temperatura = null;
        if (_zonas is not null)
        {
            var celsius = _zonas.Valores().Select(z => z.Valor / 10 - 273.15).Where(c => c > 10 && c < 130).ToList();
            if (celsius.Count > 0) temperatura = celsius.Max();
        }

        return new LecturaCpu(temperatura, carga, mhz);
    }
}
