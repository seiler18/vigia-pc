namespace Vigia.Sensores;

public sealed record LecturaCpu(double? Temperatura, double? Carga, double? FrecuenciaMhz);

public sealed record LecturaGpu(
    string Nombre,
    double? Temperatura,
    double? Carga,
    double? VramUsadaGb,
    double? VramTotalGb,
    uint? VentiladorRpm,
    double? TemperaturaAvisoFabricante);

public sealed record LecturaRam(double UsadaGb, double TotalGb)
{
    public double Porcentaje => TotalGb > 0 ? UsadaGb / TotalGb * 100 : 0;
}

public sealed record LecturaFps(string Proceso, double Fps, double? UnoPorCientoBajo);

public sealed record Lectura(
    LecturaCpu Cpu,
    LecturaGpu? Gpu,
    LecturaRam Ram,
    LecturaFps? Fps,
    string? ErrorFps,
    bool JuegoEnIntegrada);
