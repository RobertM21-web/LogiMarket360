using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Pedidos;

/// <summary>Línea de pedido con instantánea de precio del artículo (producto o kit) al registrar el pedido.</summary>
public class PedidoDetalle
{
    private readonly List<PedidoDetalleComponente> _componentes = new();

    private PedidoDetalle()
    {
    }

    internal PedidoDetalle(string codigo, string nombre, bool esKit, int cantidad, Dinero precioUnitario, IEnumerable<PedidoDetalleComponente> componentes)
    {
        Codigo = codigo;
        Nombre = nombre;
        EsKit = esKit;
        Cantidad = Guardas.Positivo(cantidad, "La cantidad de la línea");
        PrecioUnitario = precioUnitario;
        Importe = precioUnitario.Por(cantidad).Redondeado();
        _componentes.AddRange(componentes);
    }

    public int Id { get; private set; }

    public int PedidoId { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public bool EsKit { get; private set; }

    public int Cantidad { get; private set; }

    public Dinero PrecioUnitario { get; private set; }

    public Dinero Importe { get; private set; }

    /// <summary>Composición expandida a SKU finales congelada al registrar el pedido.</summary>
    public IReadOnlyCollection<PedidoDetalleComponente> Componentes => _componentes.AsReadOnly();
}

/// <summary>Instantánea de un SKU final requerido por una línea (cantidad por unidad de la línea).</summary>
public class PedidoDetalleComponente
{
    private PedidoDetalleComponente()
    {
    }

    internal PedidoDetalleComponente(int productoId, string sku, string nombre, int cantidadPorUnidad, Dinero precioUnitario)
    {
        ProductoId = productoId;
        Sku = sku;
        Nombre = nombre;
        CantidadPorUnidad = Guardas.Positivo(cantidadPorUnidad, "La cantidad del componente");
        PrecioUnitario = precioUnitario;
    }

    public int Id { get; private set; }

    public int PedidoDetalleId { get; private set; }

    public int ProductoId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public int CantidadPorUnidad { get; private set; }

    public Dinero PrecioUnitario { get; private set; }
}

/// <summary>Cambio de estado registrado para la línea de tiempo del pedido.</summary>
public class HistorialEstadoPedido
{
    private HistorialEstadoPedido()
    {
    }

    internal HistorialEstadoPedido(Estados.CodigoEstadoPedido? anterior, Estados.CodigoEstadoPedido nuevo, string usuario, DateTime fechaUtc)
    {
        EstadoAnterior = anterior;
        EstadoNuevo = nuevo;
        Usuario = usuario;
        FechaUtc = fechaUtc;
    }

    public int Id { get; private set; }

    public int PedidoId { get; private set; }

    public Estados.CodigoEstadoPedido? EstadoAnterior { get; private set; }

    public Estados.CodigoEstadoPedido EstadoNuevo { get; private set; }

    public string Usuario { get; private set; } = string.Empty;

    public DateTime FechaUtc { get; private set; }
}
