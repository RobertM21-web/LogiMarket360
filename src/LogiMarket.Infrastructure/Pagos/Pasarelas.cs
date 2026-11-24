using System.Globalization;
using LogiMarket.Application.Puertos;
using LogiMarket.Infrastructure.Pagos.Legado;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Infrastructure.Pagos;

/// <summary>Identificador de correlación que el Decorator propaga a lo largo de la cadena de cobro.</summary>
public static class ContextoCorrelacion
{
    private static readonly AsyncLocal<string?> Actual = new();

    public static string? Id
    {
        get => Actual.Value;
        set => Actual.Value = value;
    }
}

/// <summary>Contrato común de un proveedor concreto (selección y compensación por proveedor).</summary>
public interface IProveedorPago : IPasarelaPago
{
    string Nombre { get; }

    string PrefijoReferencia { get; }

    Task AnularAsync(string referencia, CancellationToken ct = default);
}

internal static class PrecondicionesCobro
{
    public static void Verificar(decimal monto, string clave)
    {
        if (monto <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monto), monto, "El monto a cobrar debe ser mayor que cero.");
        }

        if (string.IsNullOrWhiteSpace(clave))
        {
            throw new ArgumentException("La clave de idempotencia es obligatoria.", nameof(clave));
        }
    }
}

/// <summary>
/// Adapter: expone <see cref="PasarelaLegadaSimulada.CobrarCentavos"/> como <see cref="IPasarelaPago.CobrarAsync"/>
/// (decimal → centavos, texto → <see cref="ResultadoCobro"/>) sin modificar el código legado.
/// </summary>
public sealed class PasarelaLegadaAdapter : IProveedorPago
{
    private readonly PasarelaLegadaSimulada _legado;
    private readonly IRegistroAuditoria _bitacora;

    public PasarelaLegadaAdapter(PasarelaLegadaSimulada legado, IRegistroAuditoria bitacora)
    {
        _legado = legado;
        _bitacora = bitacora;
    }

    public string Nombre => "LEGACY";

    public string PrefijoReferencia => "LEG-";

    public async Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        PrecondicionesCobro.Verificar(monto, claveIdempotencia);
        var centavos = (long)Math.Round(monto * 100m, 0, MidpointRounding.AwayFromZero);
        var respuesta = _legado.CobrarCentavos(centavos, claveIdempotencia);
        var partes = respuesta.Split('|', 2);
        var resultado = partes[0] == "00"
            ? new ResultadoCobro(true, Nombre, partes[1], "Aprobado por pasarela legada")
            : ResultadoCobro.Rechazado(Nombre, partes.Length > 1 ? partes[1] : "Rechazado");
        await _bitacora.RegistrarAsync("PAGO", "PASARELA LEGACY", $"CobrarCentavos({centavos}) → {respuesta}", "sistema", resultado.Aprobado ? "APROBADO" : "RECHAZADO", ContextoCorrelacion.Id, ct);
        return resultado;
    }

    public Task AnularAsync(string referencia, CancellationToken ct = default)
    {
        var respuesta = _legado.Reversar(referencia);
        return _bitacora.RegistrarAsync("PAGO", "PASARELA LEGACY REVERSO", $"Reversar({referencia}) → {respuesta}", "sistema", respuesta == "00" ? "OK" : "ERROR", ContextoCorrelacion.Id, ct);
    }
}

/// <summary>Segundo proveedor simulado con interfaz moderna: sustituible por DI (LSP).</summary>
public sealed class PasarelaV2Simulada : IProveedorPago
{
    private readonly object _candado = new();
    private readonly Dictionary<string, ResultadoCobro> _porClave = new();
    private readonly HashSet<string> _anuladas = new();
    private readonly IRegistroAuditoria _bitacora;
    private int _secuencia;

    public PasarelaV2Simulada(IRegistroAuditoria bitacora)
    {
        _bitacora = bitacora;
    }

    public string Nombre => "V2";

    public string PrefijoReferencia => "V2-";

    public decimal LimiteUsd { get; set; } = 5_000m;

    public async Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        PrecondicionesCobro.Verificar(monto, claveIdempotencia);
        ResultadoCobro resultado;
        lock (_candado)
        {
            if (!_porClave.TryGetValue(claveIdempotencia, out resultado!))
            {
                resultado = monto > LimiteUsd
                    ? ResultadoCobro.Rechazado(Nombre, "EXCEDE LIMITE DE AUTORIZACION")
                    : new ResultadoCobro(true, Nombre, $"V2-{DateTime.UtcNow:yyyyMMdd}-{++_secuencia:0000}", "Aprobado por proveedor V2");
                _porClave[claveIdempotencia] = resultado;
            }
        }

        await _bitacora.RegistrarAsync("PAGO", "PASARELA V2", $"CobrarAsync({monto.ToString("0.00", CultureInfo.InvariantCulture)}) → {(resultado.Aprobado ? resultado.Referencia : resultado.Mensaje)}", "sistema", resultado.Aprobado ? "APROBADO" : "RECHAZADO", ContextoCorrelacion.Id, ct);
        return resultado;
    }

    public Task AnularAsync(string referencia, CancellationToken ct = default)
    {
        bool ok;
        lock (_candado)
        {
            var clave = _porClave.FirstOrDefault(e => e.Value.Referencia == referencia).Key;
            ok = clave is not null && _anuladas.Add(referencia);
            if (ok)
            {
                _porClave.Remove(clave!);
            }
        }

        return _bitacora.RegistrarAsync("PAGO", "PASARELA V2 ANULACION", referencia, "sistema", ok ? "OK" : "ERROR", ContextoCorrelacion.Id, ct);
    }
}

/// <summary>Proveedor activo seleccionado por el usuario o por el modo demostración.</summary>
public sealed class SeleccionProveedorPago : ISeleccionProveedorPago
{
    private readonly IReadOnlyList<string> _disponibles;

    public SeleccionProveedorPago(IEnumerable<string> disponibles)
    {
        _disponibles = disponibles.ToList();
        ProveedorActivo = _disponibles[0];
    }

    public string ProveedorActivo { get; private set; }

    public IReadOnlyList<string> Disponibles => _disponibles;

    public void Seleccionar(string proveedor)
    {
        if (!_disponibles.Contains(proveedor))
        {
            throw new ArgumentException($"Proveedor desconocido: {proveedor}.", nameof(proveedor));
        }

        ProveedorActivo = proveedor;
    }
}

/// <summary>Último eslabón antes de los proveedores: dirige el cobro al proveedor activo y la anulación al que la emitió.</summary>
public sealed class EnrutadorPasarelas : IPasarelaPago, IAnulacionCobro
{
    private readonly IReadOnlyDictionary<string, IProveedorPago> _proveedores;
    private readonly ISeleccionProveedorPago _seleccion;
    private readonly ConfiguracionDemostracion _demo;

    public EnrutadorPasarelas(IEnumerable<IProveedorPago> proveedores, ISeleccionProveedorPago seleccion, ConfiguracionDemostracion demo)
    {
        _proveedores = proveedores.ToDictionary(p => p.Nombre);
        _seleccion = seleccion;
        _demo = demo;
    }

    public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        var proveedor = _proveedores[_seleccion.ProveedorActivo];
        if (_demo.RechazarCobros)
        {
            // Doble de prueba del modo demostración: simula una negativa del proveedor sin llamar a la red ni alterar reglas.
            return Task.FromResult(ResultadoCobro.Rechazado(proveedor.Nombre, "RECHAZO INDUCIDO (modo demostración)"));
        }

        return proveedor.CobrarAsync(monto, claveIdempotencia, ct);
    }

    public Task AnularAsync(string referencia, string motivo, CancellationToken ct = default)
    {
        var proveedor = _proveedores.Values.FirstOrDefault(p => referencia.StartsWith(p.PrefijoReferencia, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No se reconoce el proveedor de la referencia {referencia}.");
        return proveedor.AnularAsync(referencia, ct);
    }
}

/// <summary>Reembolso simulado único (idempotente por clave).</summary>
public sealed class ServicioReembolsoSimulado : IServicioReembolso
{
    private readonly object _candado = new();
    private readonly Dictionary<string, ResultadoCobro> _porClave = new();
    private readonly IRegistroAuditoria _bitacora;
    private int _secuencia;

    public ServicioReembolsoSimulado(IRegistroAuditoria bitacora)
    {
        _bitacora = bitacora;
    }

    public async Task<ResultadoCobro> ReembolsarAsync(decimal monto, string claveReembolso, CancellationToken ct = default)
    {
        PrecondicionesCobro.Verificar(monto, claveReembolso);
        ResultadoCobro resultado;
        lock (_candado)
        {
            if (!_porClave.TryGetValue(claveReembolso, out resultado!))
            {
                resultado = new ResultadoCobro(true, "REEMBOLSO", $"RMB-{DateTime.UtcNow:yyyyMMddHHmmss}-{++_secuencia:000}", "Reembolso simulado aprobado");
                _porClave[claveReembolso] = resultado;
            }
        }

        await _bitacora.RegistrarAsync("PAGO", "REEMBOLSO SIMULADO", $"{Dinero.Formatear(monto)} → {resultado.Referencia}", "sistema", "APROBADO", claveReembolso, ct);
        return resultado;
    }
}
