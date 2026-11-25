using LogiMarket.Application.Consultas.Filtro;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Pedidos.Estados;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Application.Consultas;

public sealed record PanelGeneralDto(
    int PedidosActivos,
    int PorDespachar,
    int StockBajo,
    decimal VentasUsd,
    IReadOnlyList<PedidoResumenDto> Recientes,
    IReadOnlyList<string> Alertas);

public sealed record PuntoSerie(DateTime Dia, decimal Ventas);

public sealed record ReporteDto(DateTime Desde, DateTime Hasta, int PedidosConfirmados, decimal VentasBrutas, decimal Reembolsos, decimal VentasNetas, int Devoluciones, IReadOnlyList<PuntoSerie> Serie, IReadOnlyList<PedidoResumenDto> Pedidos);

/// <summary>Consultas (CQRS lectura) disponibles para todos los roles autenticados.</summary>
public sealed class ConsultasOperativas
{
    public const int UmbralStockBajo = 5;

    private readonly IAutorizador _autorizador;
    private readonly ILecturaPedidos _pedidos;
    private readonly ILecturaCatalogo _catalogo;
    private readonly ILecturaBitacora _bitacora;
    private readonly IReloj _reloj;

    public ConsultasOperativas(IAutorizador autorizador, ILecturaPedidos pedidos, ILecturaCatalogo catalogo, ILecturaBitacora bitacora, IReloj reloj)
    {
        _reloj = reloj;
        _autorizador = autorizador;
        _pedidos = pedidos;
        _catalogo = catalogo;
        _bitacora = bitacora;
    }

    public async Task<IReadOnlyList<PedidoResumenDto>> PedidosAsync(string? filtro = null, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Pedidos", ct);
        var todos = await _pedidos.ListarAsync(ct);
        return string.IsNullOrWhiteSpace(filtro) ? todos : InterpreteFiltro.Filtrar(todos, filtro);
    }

    public async Task<PedidoDetalleDto?> PedidoAsync(int id, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, $"Pedido {id}", ct);
        return await _pedidos.ObtenerAsync(id, ct);
    }

    public async Task<IReadOnlyList<ProductoDto>> ProductosAsync(CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Productos", ct);
        return await _catalogo.ListarProductosAsync(ct);
    }

    public async Task<IReadOnlyList<KitDto>> KitsAsync(CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Kits", ct);
        return await _catalogo.ListarKitsAsync(ct);
    }

    public async Task<IReadOnlyList<ClienteDto>> ClientesAsync(CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Clientes", ct);
        return await _catalogo.ListarClientesAsync(ct);
    }

    public async Task<IReadOnlyList<MovimientoDto>> MovimientosAsync(int maximo = 200, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Movimientos", ct);
        return await _bitacora.ListarMovimientosAsync(maximo, ct);
    }

    public async Task<IReadOnlyList<AuditoriaDto>> AuditoriaAsync(int maximo = 300, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Auditoría", ct);
        return await _bitacora.ListarAuditoriaAsync(maximo, ct);
    }

    public async Task<IReadOnlyList<OutboxDto>> OutboxAsync(CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Outbox", ct);
        return await _bitacora.ListarOutboxAsync(ct);
    }

    public async Task<IReadOnlyList<NotificacionDto>> NotificacionesAsync(int maximo = 200, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Notificaciones", ct);
        return await _bitacora.ListarNotificacionesAsync(maximo, ct);
    }

    public async Task<PanelGeneralDto> PanelAsync(CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Panel general", ct);
        var pedidos = await _pedidos.ListarAsync(ct);
        var productos = await _catalogo.ListarProductosAsync(ct);
        var outbox = await _bitacora.ListarOutboxAsync(ct);

        var bajos = productos.Where(p => p.Activo && p.Stock <= UmbralStockBajo).ToList();
        var pendientes = outbox.Count(o => o.Estado == "PENDIENTE");
        var porRevisar = pedidos.Count(p => p.Estado == NombresEstado.Entregado && !p.Devuelto && p.FechaEntregaUtc >= _reloj.AhoraUtc.AddDays(-30));
        var alertas = new List<string>();
        if (bajos.Count > 0)
        {
            alertas.Add($"{bajos.Count} producto(s) con stock bajo: {string.Join(", ", bajos.Select(b => $"{b.Sku}={b.Stock}"))}");
        }

        if (pendientes > 0)
        {
            alertas.Add($"{pendientes} evento(s) Outbox pendiente(s)");
        }

        if (porRevisar > 0)
        {
            alertas.Add($"{porRevisar} pedido(s) entregado(s) dentro del plazo de devolución");
        }

        if (alertas.Count == 0)
        {
            alertas.Add("Sin alertas.");
        }

        return new PanelGeneralDto(
            pedidos.Count(p => NombresEstado.Activos.Contains(p.Estado)),
            pedidos.Count(p => p.Estado == NombresEstado.Pagado),
            bajos.Count,
            VentasNetas(pedidos),
            pedidos.OrderByDescending(p => p.FechaCreacionUtc).Take(10).ToList(),
            alertas);
    }

    public async Task<ReporteDto> ReporteAsync(DateTime desdeLocal, DateTime hastaLocal, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Reporte", ct);
        var desde = desdeLocal.Date.ToUniversalTime();
        var hasta = hastaLocal.Date.AddDays(1).ToUniversalTime();
        var pedidos = await _pedidos.ListarAsync(ct);
        var pagados = pedidos.Where(p => p.FechaPagoUtc >= desde && p.FechaPagoUtc < hasta).ToList();
        var brutas = pagados.Sum(p => p.Total);
        var reembolsos = pagados.Where(p => p.Devuelto).Sum(p => p.Reembolso);
        var serie = pagados
            .GroupBy(p => p.FechaPagoUtc!.Value.ToLocalTime().Date)
            .OrderBy(g => g.Key)
            .Select(g => new PuntoSerie(g.Key, g.Sum(p => p.Total - p.Reembolso)))
            .ToList();
        return new ReporteDto(desdeLocal.Date, hastaLocal.Date, pagados.Count, brutas, reembolsos, brutas - reembolsos, pagados.Count(p => p.Devuelto), serie, pagados);
    }

    private static decimal VentasNetas(IEnumerable<PedidoResumenDto> pedidos) =>
        pedidos.Where(p => p.FechaPagoUtc is not null).Sum(p => p.Total - p.Reembolso);
}
