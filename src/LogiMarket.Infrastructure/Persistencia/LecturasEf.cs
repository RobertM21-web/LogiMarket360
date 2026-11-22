using LogiMarket.Application.Consultas;
using LogiMarket.Domain.Pedidos;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>Lado de lectura: consultas sin rastreo proyectadas a DTO.</summary>
public sealed class LecturasEf(LogiMarketDbContext db) : ILecturaPedidos, ILecturaCatalogo, ILecturaBitacora
{
    public async Task<IReadOnlyList<PedidoResumenDto>> ListarAsync(CancellationToken ct = default)
    {
        var pedidos = await db.Pedidos.AsNoTracking()
            .Include(p => p.Despacho)
            .Include(p => p.Devolucion)
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);
        return pedidos.Select(Resumen).ToList();
    }

    public async Task<PedidoDetalleDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Pedidos.AsNoTracking()
            .Include(x => x.Detalles).ThenInclude(d => d.Componentes)
            .Include(x => x.Historial)
            .Include(x => x.Pago)
            .Include(x => x.Despacho)
            .Include(x => x.Devolucion)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null)
        {
            return null;
        }

        return new PedidoDetalleDto(
            Resumen(p),
            p.Detalles.Select(d => new LineaPedidoDto(
                d.Codigo, d.Nombre, d.EsKit, d.Cantidad, d.PrecioUnitario.Valor, d.Importe.Valor,
                string.Join(", ", d.Componentes.Select(c => $"{c.Sku}×{c.CantidadPorUnidad}")))).ToList(),
            p.Historial.OrderBy(h => h.FechaUtc).ThenBy(h => h.Id)
                .Select(h => new HistorialDto(h.EstadoAnterior?.ToString().ToUpperInvariant(), h.EstadoNuevo.ToString().ToUpperInvariant(), h.Usuario, h.FechaUtc)).ToList(),
            p.Pago?.Referencia,
            p.Pago?.Proveedor,
            p.Despacho is { } d ? new DespachoDto(d.NumeroGuia, d.Etiqueta, d.PlazoDias, d.FechaDespachoUtc, d.FechaEstimadaUtc, d.FechaEntregaUtc) : null,
            p.Devolucion is { } v ? new DevolucionDto(v.Codigo, v.Motivo.ToString().ToUpperInvariant(), v.DiasDesdeEntrega, v.MontoReembolso.Valor, v.ReintegraStock, v.ReferenciaReembolso, v.FechaUtc) : null);
    }

    public async Task<IReadOnlyList<ProductoDto>> ListarProductosAsync(CancellationToken ct = default) =>
        (await db.Productos.AsNoTracking().OrderBy(p => p.Sku).ToListAsync(ct))
            .Select(p => new ProductoDto(p.Id, p.Sku, p.Nombre, p.Categoria, p.Descripcion, p.Precio.Valor, p.PesoKg, p.Stock, p.Activo))
            .ToList();

    public async Task<IReadOnlyList<KitDto>> ListarKitsAsync(CancellationToken ct = default)
    {
        var kits = await db.Kits.AsNoTracking().Include(k => k.Componentes).OrderBy(k => k.Codigo).ToListAsync(ct);
        var productos = await db.Productos.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        var porId = kits.ToDictionary(k => k.Id);
        return kits.Select(k => new KitDto(
            k.Id, k.Codigo, k.Nombre, k.Descripcion, k.Activo,
            k.Componentes.OrderBy(c => c.Id).Select(c => c.ProductoId is int pid
                ? new ComponenteKitDto(pid, null, productos[pid].Sku, productos[pid].Nombre, c.Cantidad)
                : new ComponenteKitDto(null, c.KitHijoId, porId[c.KitHijoId!.Value].Codigo, porId[c.KitHijoId.Value].Nombre, c.Cantidad)).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<ClienteDto>> ListarClientesAsync(CancellationToken ct = default) =>
        (await db.Clientes.AsNoTracking().OrderBy(c => c.Codigo).ToListAsync(ct))
            .Select(c => new ClienteDto(c.Id, c.Codigo, c.Nombre, c.Identificacion, c.Categoria.ToString().ToUpperInvariant(), c.Email, c.Telefono, c.CanalPreferido.ToString().ToUpperInvariant(), c.Activo))
            .ToList();

    public async Task<IReadOnlyList<MovimientoDto>> ListarMovimientosAsync(int maximo, CancellationToken ct = default) =>
        await db.Movimientos.AsNoTracking()
            .OrderByDescending(m => m.Id).Take(maximo)
            .Select(m => new MovimientoDto(m.Id, m.FechaUtc, m.Sku, m.Producto!.Nombre, m.Cantidad, m.Motivo.ToString(), m.Referencia, m.Usuario, m.StockResultante))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AuditoriaDto>> ListarAuditoriaAsync(int maximo, CancellationToken ct = default) =>
        await db.Auditoria.AsNoTracking()
            .OrderByDescending(a => a.Id).Take(maximo)
            .Select(a => new AuditoriaDto(a.Id, a.FechaUtc, a.Categoria, a.Evento, a.Resultado, a.Detalle, a.Usuario, a.Correlacion))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OutboxDto>> ListarOutboxAsync(CancellationToken ct = default) =>
        (await db.Outbox.AsNoTracking().OrderByDescending(e => e.Id).Take(500).ToListAsync(ct))
            .Select(e => new OutboxDto(e.Id, e.Tipo, e.Referencia, e.Estado.ToString().ToUpperInvariant(), e.Intentos, e.UltimoError, e.CreadoUtc, e.ProcesadoUtc, e.ObservadoresCompletados))
            .ToList();

    public async Task<IReadOnlyList<NotificacionDto>> ListarNotificacionesAsync(int maximo, CancellationToken ct = default) =>
        await db.Notificaciones.AsNoTracking()
            .OrderByDescending(n => n.Id).Take(maximo)
            .Select(n => new NotificacionDto(n.Id, n.FechaUtc, n.Canal, n.Destinatario, n.Asunto, n.Mensaje, n.Referencia))
            .ToListAsync(ct);

    private static PedidoResumenDto Resumen(Pedido p) => new(
        p.Id,
        p.Codigo,
        p.ClienteId,
        p.NombreCliente,
        p.CategoriaCliente.ToString().ToUpperInvariant(),
        p.CodigoEstado.ToString().ToUpperInvariant(),
        p.PoliticaDescuento,
        p.Subtotal.Valor,
        p.Descuento.Valor,
        p.TarifaEntrega.Valor,
        p.Total.Valor,
        p.TipoEntrega == Domain.Entrega.TipoEntrega.Estandar ? "ESTÁNDAR" : "EXPRÉS",
        p.FechaCreacionUtc,
        p.FechaPagoUtc,
        p.FechaEntregaUtc,
        p.Devolucion is not null,
        p.Devolucion?.MontoReembolso.Valor ?? 0m,
        p.Despacho?.NumeroGuia);
}
