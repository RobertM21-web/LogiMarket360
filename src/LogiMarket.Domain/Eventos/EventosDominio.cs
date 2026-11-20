namespace LogiMarket.Domain.Eventos;

/// <summary>Hecho de negocio ocurrido en el dominio; se persiste en el Outbox en la misma transacción.</summary>
public interface IEventoDominio
{
    string Tipo { get; }

    DateTime OcurridoUtc { get; }
}

public sealed record PedidoPagado(int PedidoId, string CodigoPedido, int ClienteId, decimal Total, string ReferenciaCobro, string Usuario, DateTime OcurridoUtc)
    : IEventoDominio
{
    public string Tipo => nameof(PedidoPagado);
}

public sealed record DespachoRegistrado(int PedidoId, string CodigoPedido, int ClienteId, string NumeroGuia, DateTime FechaEstimadaUtc, string Usuario, DateTime OcurridoUtc)
    : IEventoDominio
{
    public string Tipo => nameof(DespachoRegistrado);
}

public sealed record DevolucionAceptada(int PedidoId, string CodigoPedido, int ClienteId, string CodigoDevolucion, decimal MontoReembolso, string Usuario, DateTime OcurridoUtc)
    : IEventoDominio
{
    public string Tipo => nameof(DevolucionAceptada);
}
