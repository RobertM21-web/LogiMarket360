using LogiMarket.Domain.Entrega;

namespace LogiMarket.Domain.Pedidos;

/// <summary>Registro logístico del pedido con la guía simulada de su familia de entrega.</summary>
public class Despacho
{
    private Despacho()
    {
    }

    internal Despacho(Pedido pedido, GuiaEnvio guia, string usuario, DateTime fechaUtc)
    {
        Pedido = pedido;
        PedidoId = pedido.Id;
        NumeroGuia = guia.Numero;
        TipoEntrega = guia.Familia;
        Etiqueta = guia.Etiqueta;
        PlazoDias = guia.PlazoDias;
        FechaEstimadaUtc = guia.FechaEstimadaUtc;
        FechaDespachoUtc = fechaUtc;
        UsuarioDespacho = usuario;
    }

    public int Id { get; private set; }

    public int PedidoId { get; private set; }

    public Pedido? Pedido { get; private set; }

    public string NumeroGuia { get; private set; } = string.Empty;

    public TipoEntrega TipoEntrega { get; private set; }

    public string Etiqueta { get; private set; } = string.Empty;

    public int PlazoDias { get; private set; }

    public DateTime FechaDespachoUtc { get; private set; }

    public DateTime FechaEstimadaUtc { get; private set; }

    public DateTime? FechaEntregaUtc { get; private set; }

    public string UsuarioDespacho { get; private set; } = string.Empty;

    public string? UsuarioEntrega { get; private set; }

    internal void RegistrarEntrega(string usuario, DateTime fechaUtc)
    {
        FechaEntregaUtc = fechaUtc;
        UsuarioEntrega = usuario;
    }
}
