using Vigia.Nativo;

namespace Vigia.Sensores;

/// <summary>
/// Temperatura y ventilador por D3DKMT (lo mismo que el Administrador de
/// tareas); carga y VRAM por los contadores «GPU Engine» y «GPU Adapter
/// Memory», que Windows lleva por adaptador y por proceso.
///
/// En un notebook con gráficos híbridos se sigue a la discreta, que es la que
/// debería mover el juego. Y se avisa si el juego está corriendo en la
/// integrada: pasa cuando Windows o el panel de NVIDIA no lo asignaron bien, y
/// se nota como FPS bajos sin motivo aparente.
/// </summary>
internal sealed class SensorGpu : IDisposable
{
    private sealed record Adaptador(uint Handle, string Clave, string Nombre, double VramTotalGb, bool Discreta);

    private readonly Adaptador? _principal;
    private readonly List<Adaptador> _adaptadores = [];
    private readonly double? _temperaturaAviso;
    private readonly ContadorPdh? _motores3d;
    private readonly ContadorPdh? _vramUsada;
    private List<(string Instancia, double Valor)> _motoresAnteriores = [];

    public SensorGpu(ConsultaPdh consulta)
    {
        foreach (var info in D3dkmt.Enumerar())
        {
            var tipo = default(D3dkmt.TipoAdaptador);
            D3dkmt.Consultar(info.Handle, D3dkmt.KMTQAITYPE_ADAPTERTYPE, ref tipo);
            if (tipo.HasFlag(D3dkmt.TipoAdaptador.Software) || !tipo.HasFlag(D3dkmt.TipoAdaptador.Render))
            {
                D3dkmt.Cerrar(info.Handle);
                continue;
            }

            var segmentos = default(D3dkmt.Segmentos);
            D3dkmt.Consultar(info.Handle, D3dkmt.KMTQAITYPE_GETSEGMENTSIZE, ref segmentos);

            _adaptadores.Add(new Adaptador(
                info.Handle,
                ClaveLuid(info.Luid),
                Limpiar(D3dkmt.Nombre(info.Handle) ?? "GPU"),
                segmentos.MemoriaDedicada / (1024d * 1024 * 1024),
                tipo.HasFlag(D3dkmt.TipoAdaptador.HibridaDiscreta)));
        }

        // La discreta: la marcada como híbrida discreta o, en un PC de
        // escritorio sin esa marca, la de más memoria dedicada.
        _principal = _adaptadores.OrderByDescending(a => a.Discreta).ThenByDescending(a => a.VramTotalGb).FirstOrDefault();

        if (_principal is not null)
        {
            var capacidades = default(D3dkmt.CapacidadesRendimiento);
            if (D3dkmt.Consultar(_principal.Handle, D3dkmt.KMTQAITYPE_ADAPTERPERFDATA_CAPS, ref capacidades) && capacidades.TemperaturaAviso > 0)
                _temperaturaAviso = capacidades.TemperaturaAviso / 10d;
        }

        _motores3d = consulta.Agregar(@"\GPU Engine(*engtype_3D)\Utilization Percentage");
        _vramUsada = consulta.Agregar(@"\GPU Adapter Memory(*)\Dedicated Usage");
    }

    public (LecturaGpu? Gpu, bool JuegoEnIntegrada) Leer(int? pidJuego)
    {
        if (_principal is null) return (null, false);

        // Cuando un proceso abre o cierra la GPU, Windows rehace la lista de
        // instancias y esa muestra sale vacía: se repite la anterior en vez de
        // marcar un 0 % falso en plena partida.
        var motores = _motores3d?.Valores() ?? [];
        if (motores.Count == 0) motores = _motoresAnteriores;
        else _motoresAnteriores = motores;

        double CargaDe(Adaptador a, string? prefijoPid = null) => Math.Min(100, motores
            .Where(m => m.Instancia.Contains(a.Clave, StringComparison.OrdinalIgnoreCase)
                        && (prefijoPid is null || m.Instancia.StartsWith(prefijoPid, StringComparison.Ordinal)))
            .Sum(m => m.Valor));

        double? vramUsada = _vramUsada?.Valores()
            .Where(v => v.Instancia.Contains(_principal.Clave, StringComparison.OrdinalIgnoreCase))
            .Sum(v => v.Valor) / (1024d * 1024 * 1024);

        var rendimiento = default(D3dkmt.Rendimiento);
        bool hayDatos = D3dkmt.Consultar(_principal.Handle, D3dkmt.KMTQAITYPE_ADAPTERPERFDATA, ref rendimiento);
        // 0 = la GPU no informa o está apagada (en un notebook, la discreta se
        // apaga cuando nadie la usa).
        double? temperatura = hayDatos && rendimiento.Temperatura > 0 ? rendimiento.Temperatura / 10d : null;
        uint? ventilador = hayDatos && rendimiento.VentiladorRpm > 0 ? rendimiento.VentiladorRpm : null;

        var gpu = new LecturaGpu(
            _principal.Nombre,
            temperatura,
            CargaDe(_principal),
            vramUsada,
            _principal.VramTotalGb > 0 ? _principal.VramTotalGb : null,
            ventilador,
            _temperaturaAviso);

        bool enIntegrada = false;
        if (pidJuego is int pid)
        {
            string prefijo = $"pid_{pid}_";
            double enPrincipal = CargaDe(_principal, prefijo);
            double enOtras = _adaptadores.Where(a => a != _principal).Select(a => CargaDe(a, prefijo)).DefaultIfEmpty(0).Max();
            enIntegrada = enOtras > 10 && enOtras > enPrincipal * 2;
        }

        return (gpu, enIntegrada);
    }

    private static string ClaveLuid(D3dkmt.Luid luid) => $"luid_0x{luid.Alto:X8}_0x{luid.Bajo:X8}";

    // «NVIDIA GeForce RTX 3050 Laptop GPU» no cabe en un panel chico y el
    // fabricante no aporta: queda «RTX 3050 Laptop».
    private static string Limpiar(string nombre)
    {
        foreach (var ruido in new[] { "NVIDIA ", "GeForce ", "AMD ", "Intel(R) ", "(TM)", "(R)", " GPU" })
            nombre = nombre.Replace(ruido, "", StringComparison.OrdinalIgnoreCase);
        return nombre.Trim();
    }

    public void Dispose()
    {
        foreach (var a in _adaptadores) D3dkmt.Cerrar(a.Handle);
        _adaptadores.Clear();
    }
}
