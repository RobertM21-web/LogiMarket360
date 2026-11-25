using LogiMarket.Application.Metricas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Application.Pagos;

/// <summary>
/// Proxy de protección: implementa <see cref="IPasarelaPago"/> y verifica que el rol sea VENTAS o ADMIN
/// antes de delegar en el Decorator y el Adapter. Un rechazo nunca alcanza al proveedor ni genera LOG de cobro.
/// </summary>
public sealed class PagoAutorizadoProxy : IPasarelaPago
{
    private readonly IPasarelaPago _real;
    private readonly IAutorizador _autorizador;
    private readonly MetricasSesion _metricas;

    public PagoAutorizadoProxy(IPasarelaPago real, IAutorizador autorizador, MetricasSesion metricas)
    {
        _real = real;
        _autorizador = autorizador;
        _metricas = metricas;
    }

    public async Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        try
        {
            await _autorizador.VerificarAsync(Operacion.CobrarPedido, $"Cobro {Dinero.Formatear(monto)} clave {claveIdempotencia}", ct);
        }
        catch (AccesoDenegadoException)
        {
            _metricas.RegistrarCobroDenegado();
            throw;
        }

        return await _real.CobrarAsync(monto, claveIdempotencia, ct);
    }
}
