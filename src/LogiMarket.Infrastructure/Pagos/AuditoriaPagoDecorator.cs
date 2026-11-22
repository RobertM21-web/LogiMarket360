using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;

namespace LogiMarket.Infrastructure.Pagos;

/// <summary>
/// Decorator: envuelve cualquier <see cref="IPasarelaPago"/> y registra INICIO, resultado (FIN) o ERROR
/// con un id de correlación, sin alterar el resultado ni las excepciones del cobro.
/// </summary>
public sealed class AuditoriaPagoDecorator : IPasarelaPago
{
    private readonly IPasarelaPago _interna;
    private readonly IRegistroAuditoria _bitacora;
    private readonly ISesionActual _sesion;

    public AuditoriaPagoDecorator(IPasarelaPago interna, IRegistroAuditoria bitacora, ISesionActual sesion)
    {
        _interna = interna;
        _bitacora = bitacora;
        _sesion = sesion;
    }

    public async Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        var correlacion = $"COR-{Guid.NewGuid():N}"[..16];
        var anterior = ContextoCorrelacion.Id;
        ContextoCorrelacion.Id = correlacion;
        var usuario = _sesion.Usuario?.NombreUsuario ?? "sistema";
        try
        {
            await _bitacora.RegistrarAsync("PAGO", "COBRO INICIO", $"USD {monto:0.00} clave {claveIdempotencia}", usuario, "INICIO", correlacion, ct);
            var resultado = await _interna.CobrarAsync(monto, claveIdempotencia, ct);
            await _bitacora.RegistrarAsync(
                "PAGO",
                "COBRO FIN",
                resultado.Aprobado ? $"{resultado.Proveedor} ref. {resultado.Referencia}" : $"{resultado.Proveedor}: {resultado.Mensaje}",
                usuario,
                resultado.Aprobado ? "COBRO APROBADO" : "COBRO RECHAZADO",
                correlacion,
                ct);
            return resultado;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _bitacora.RegistrarAsync("PAGO", "COBRO ERROR", ex.Message, usuario, "ERROR", correlacion, CancellationToken.None);
            throw;
        }
        finally
        {
            ContextoCorrelacion.Id = anterior;
        }
    }
}
