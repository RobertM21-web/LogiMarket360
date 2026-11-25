using LogiMarket.Domain.Catalogo.Recorrido;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Pedidos.Estados;
using LogiMarket.Domain.Promociones;

namespace LogiMarket.Domain.Pedidos;

/// <summary>Contenido valorado de un pedido producido por el <see cref="PedidoBuilder"/>.</summary>
public sealed record ContenidoPedido(Cliente Cliente, TipoEntrega TipoEntrega, IReadOnlyList<LineaValoracion> Lineas, ResumenPedido Resumen);

/// <summary>Requerimiento de stock por producto final, derivado de la instantánea del pedido.</summary>
public sealed record RequerimientoStock(int ProductoId, string Sku, int Cantidad);

/// <summary>
/// Agregado Pedido: instantánea de precios y composición, totales, ciclo de vida (State), pago único,
/// despacho, devolución única y eventos de dominio. No expone setters públicos.
/// </summary>
public class Pedido
{
    private readonly List<PedidoDetalle> _detalles = new();
    private readonly List<HistorialEstadoPedido> _historial = new();
    private readonly List<IEventoDominio> _eventos = new();

    private Pedido()
    {
    }

    internal Pedido(string codigo, ContenidoPedido contenido, string usuario, DateTime ahoraUtc)
    {
        Codigo = Guardas.Texto(codigo, "Código de pedido", 20);
        ClaveIdempotencia = Guid.NewGuid().ToString("N");
        CreadoPor = usuario;
        FechaCreacionUtc = ahoraUtc;
        CodigoEstado = CodigoEstadoPedido.Borrador;
        _historial.Add(new HistorialEstadoPedido(null, CodigoEstadoPedido.Borrador, usuario, ahoraUtc));
        AplicarContenido(contenido);
    }

    public int Id { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public int ClienteId { get; private set; }

    public Cliente? Cliente { get; private set; }

    public string NombreCliente { get; private set; } = string.Empty;

    public CategoriaCliente CategoriaCliente { get; private set; }

    public TipoEntrega TipoEntrega { get; private set; }

    public Dinero Subtotal { get; private set; }

    public string PoliticaDescuento { get; private set; } = string.Empty;

    public decimal TasaDescuento { get; private set; }

    public Dinero Descuento { get; private set; }

    public Dinero TarifaEntrega { get; private set; }

    public Dinero Total { get; private set; }

    public int UnidadesFisicas { get; private set; }

    public CodigoEstadoPedido CodigoEstado { get; private set; }

    public string ClaveIdempotencia { get; private set; } = string.Empty;

    public string CreadoPor { get; private set; } = string.Empty;

    public DateTime FechaCreacionUtc { get; private set; }

    public DateTime? FechaValidacionUtc { get; internal set; }

    public DateTime? FechaPagoUtc { get; internal set; }

    public DateTime? FechaDespachoUtc { get; internal set; }

    public DateTime? FechaEntregaUtc { get; internal set; }

    public DateTime? FechaCancelacionUtc { get; internal set; }

    public Pago? Pago { get; private set; }

    public Despacho? Despacho { get; private set; }

    public Devolucion? Devolucion { get; private set; }

    public EstadoPedido Estado => EstadoPedido.De(CodigoEstado);

    public bool FueDevuelto => Devolucion is not null;

    /// <summary>Importe de productos después del descuento, sin tarifa de envío (base del reembolso).</summary>
    public Dinero ImporteProductos => Subtotal - Descuento;

    public IReadOnlyCollection<PedidoDetalle> Detalles => _detalles.AsReadOnly();

    public IReadOnlyCollection<HistorialEstadoPedido> Historial => _historial.AsReadOnly();

    public IReadOnlyCollection<IEventoDominio> Eventos => _eventos.AsReadOnly();

    public void ReemplazarContenido(ContenidoPedido contenido)
    {
        Estado.EditarContenido(this);
        AplicarContenido(contenido);
    }

    public void Validar(string usuario, DateTime ahoraUtc) => Estado.Validar(this, usuario, ahoraUtc);

    /// <summary>Registra el cobro aprobado. Rechaza un segundo cobro y cualquier estado distinto de VALIDADO.</summary>
    public Pago RegistrarPago(string proveedor, string referencia, string usuario, DateTime ahoraUtc)
    {
        if (Pago is not null)
        {
            throw new ReglaNegocioException("COBRO_DUPLICADO", $"El pedido {Codigo} ya tiene un cobro aprobado ({Pago.Referencia}).");
        }

        Estado.RegistrarPago(this, usuario, ahoraUtc);
        Pago = new Pago(this, proveedor, referencia, usuario, ahoraUtc);
        _eventos.Add(new PedidoPagado(Id, Codigo, ClienteId, Total.Valor, referencia, usuario, ahoraUtc));
        return Pago;
    }

    public Despacho Despachar(GuiaEnvio guia, string usuario, DateTime ahoraUtc)
    {
        if (guia.Familia != TipoEntrega)
        {
            throw new ReglaNegocioException("FAMILIA_INCOMPATIBLE", $"El pedido {Codigo} es {TipoEntrega}; la guía es {guia.Familia}.");
        }

        Estado.Despachar(this, usuario, ahoraUtc);
        Despacho = new Despacho(this, guia, usuario, ahoraUtc);
        _eventos.Add(new DespachoRegistrado(Id, Codigo, ClienteId, guia.Numero, guia.FechaEstimadaUtc, usuario, ahoraUtc));
        return Despacho;
    }

    public void Entregar(string usuario, DateTime ahoraUtc)
    {
        Estado.Entregar(this, usuario, ahoraUtc);
        Despacho?.RegistrarEntrega(usuario, ahoraUtc);
    }

    public void Cancelar(string usuario, DateTime ahoraUtc) => Estado.Cancelar(this, usuario, ahoraUtc);

    /// <summary>Registra la devolución total única con reembolso = importe de productos tras descuento (sin envío).</summary>
    public Devolucion RegistrarDevolucion(string codigo, MotivoDevolucion motivo, bool reintegraStock, string referenciaReembolso, string observacion, string usuario, DateTime ahoraUtc)
    {
        Estado.Devolver(this);
        if (Devolucion is not null)
        {
            throw new ReglaNegocioException("DEVOLUCION_DUPLICADA", $"El pedido {Codigo} ya fue devuelto ({Devolucion.Codigo}).");
        }

        Devolucion = new Devolucion(this, codigo, motivo, DiasDesdeEntrega(ahoraUtc), ImporteProductos, reintegraStock, referenciaReembolso, observacion, usuario, ahoraUtc);
        _eventos.Add(new DevolucionAceptada(Id, Codigo, ClienteId, codigo, ImporteProductos.Valor, usuario, ahoraUtc));
        return Devolucion;
    }

    public int DiasDesdeEntrega(DateTime ahoraUtc) =>
        FechaEntregaUtc is DateTime entrega ? (int)Math.Floor((ahoraUtc - entrega).TotalDays) : -1;

    /// <summary>Cantidades por SKU final según la instantánea (no según el catálogo actual).</summary>
    public IReadOnlyList<RequerimientoStock> RequerimientosStock() =>
        _detalles
            .SelectMany(d => d.Componentes.Select(c => (c.ProductoId, c.Sku, Cantidad: c.CantidadPorUnidad * d.Cantidad)))
            .GroupBy(x => (x.ProductoId, x.Sku))
            .Select(g => new RequerimientoStock(g.Key.ProductoId, g.Key.Sku, g.Sum(x => x.Cantidad)))
            .OrderBy(r => r.Sku, StringComparer.Ordinal)
            .ToList();

    public void LimpiarEventos() => _eventos.Clear();

    internal void TransicionarA(CodigoEstadoPedido nuevo, string usuario, DateTime ahoraUtc)
    {
        _historial.Add(new HistorialEstadoPedido(CodigoEstado, nuevo, usuario, ahoraUtc));
        CodigoEstado = nuevo;
        EstadoPedido.De(nuevo).AlEntrar(this, ahoraUtc);
    }

    private void AplicarContenido(ContenidoPedido contenido)
    {
        Cliente = contenido.Cliente;
        ClienteId = contenido.Cliente.Id;
        NombreCliente = contenido.Cliente.Nombre;
        CategoriaCliente = contenido.Cliente.Categoria;
        TipoEntrega = contenido.TipoEntrega;
        var r = contenido.Resumen;
        Subtotal = r.Subtotal;
        PoliticaDescuento = r.Politica;
        TasaDescuento = r.Tasa;
        Descuento = r.Descuento;
        TarifaEntrega = r.Tarifa;
        Total = r.Total;
        UnidadesFisicas = r.UnidadesFisicas;

        _detalles.Clear();
        foreach (var linea in contenido.Lineas)
        {
            var componentes = ExpansionSku.Expandir(linea.Elemento, 1)
                .Select(req => new PedidoDetalleComponente(req.Producto.ProductoId, req.Producto.Sku, req.Producto.Nombre, req.Cantidad, req.Producto.Precio));
            _detalles.Add(new PedidoDetalle(linea.Elemento.Codigo, linea.Elemento.Nombre, linea.Elemento.EsKit, linea.Cantidad, linea.Elemento.Precio, componentes));
        }
    }
}
