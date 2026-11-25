using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Pedidos;

public enum MotivoDevolucion
{
    Defecto = 0,
    Arrepentimiento = 1,
}

/// <summary>Devolución total aceptada. Única por pedido; el reembolso excluye la tarifa de envío.</summary>
public class Devolucion
{
    private Devolucion()
    {
    }

    internal Devolucion(Pedido pedido, string codigo, MotivoDevolucion motivo, int diasDesdeEntrega, Dinero montoReembolso, bool reintegraStock, string referenciaReembolso, string observacion, string usuario, DateTime fechaUtc)
    {
        Pedido = pedido;
        PedidoId = pedido.Id;
        Codigo = Guardas.Texto(codigo, "Código de devolución", 20);
        Motivo = motivo;
        DiasDesdeEntrega = diasDesdeEntrega;
        MontoReembolso = montoReembolso;
        ReintegraStock = reintegraStock;
        ReferenciaReembolso = Guardas.Texto(referenciaReembolso, "Referencia de reembolso", 60);
        Observacion = observacion?.Trim() ?? string.Empty;
        Usuario = usuario;
        FechaUtc = fechaUtc;
    }

    public int Id { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public int PedidoId { get; private set; }

    public Pedido? Pedido { get; private set; }

    public MotivoDevolucion Motivo { get; private set; }

    public int DiasDesdeEntrega { get; private set; }

    public Dinero MontoReembolso { get; private set; }

    public bool ReintegraStock { get; private set; }

    public string ReferenciaReembolso { get; private set; } = string.Empty;

    public string Observacion { get; private set; } = string.Empty;

    public string Usuario { get; private set; } = string.Empty;

    public DateTime FechaUtc { get; private set; }
}
