using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Pedidos;

/// <summary>Cobro simulado aprobado. Único por pedido, por referencia y por clave de idempotencia.</summary>
public class Pago
{
    private Pago()
    {
    }

    internal Pago(Pedido pedido, string proveedor, string referencia, string usuario, DateTime fechaUtc)
    {
        Pedido = pedido;
        PedidoId = pedido.Id;
        Monto = pedido.Total;
        Proveedor = Guardas.Texto(proveedor, "Proveedor", 40);
        Referencia = Guardas.Texto(referencia, "Referencia de cobro", 60);
        ClaveIdempotencia = pedido.ClaveIdempotencia;
        Usuario = usuario;
        FechaUtc = fechaUtc;
    }

    public int Id { get; private set; }

    public int PedidoId { get; private set; }

    public Pedido? Pedido { get; private set; }

    public Dinero Monto { get; private set; }

    public string Proveedor { get; private set; } = string.Empty;

    public string Referencia { get; private set; } = string.Empty;

    public string ClaveIdempotencia { get; private set; } = string.Empty;

    public string Usuario { get; private set; } = string.Empty;

    public DateTime FechaUtc { get; private set; }
}
