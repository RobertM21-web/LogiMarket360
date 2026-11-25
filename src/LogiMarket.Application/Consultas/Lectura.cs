using LogiMarket.Application.Consultas.Filtro;

namespace LogiMarket.Application.Consultas;

public sealed record PedidoResumenDto(
    int Id,
    string Codigo,
    int ClienteId,
    string Cliente,
    string Categoria,
    string Estado,
    string Politica,
    decimal Subtotal,
    decimal Descuento,
    decimal Tarifa,
    decimal Total,
    string Entrega,
    DateTime FechaCreacionUtc,
    DateTime? FechaPagoUtc,
    DateTime? FechaEntregaUtc,
    bool Devuelto,
    decimal Reembolso,
    string? Guia) : IPedidoFiltrable;

public sealed record LineaPedidoDto(string Codigo, string Nombre, bool EsKit, int Cantidad, decimal PrecioUnitario, decimal Importe, string Componentes);

public sealed record HistorialDto(string? Anterior, string Nuevo, string Usuario, DateTime FechaUtc);

public sealed record DespachoDto(string Guia, string Etiqueta, int PlazoDias, DateTime FechaDespachoUtc, DateTime FechaEstimadaUtc, DateTime? FechaEntregaUtc);

public sealed record DevolucionDto(string Codigo, string Motivo, int DiasDesdeEntrega, decimal Monto, bool ReintegraStock, string ReferenciaReembolso, DateTime FechaUtc);

public sealed record PedidoDetalleDto(
    PedidoResumenDto Resumen,
    IReadOnlyList<LineaPedidoDto> Lineas,
    IReadOnlyList<HistorialDto> Historial,
    string? ReferenciaCobro,
    string? Proveedor,
    DespachoDto? Despacho,
    DevolucionDto? Devolucion);

public sealed record ProductoDto(int Id, string Sku, string Nombre, string Categoria, string Descripcion, decimal Precio, decimal PesoKg, int Stock, bool Activo);

public sealed record ComponenteKitDto(int? ProductoId, int? KitHijoId, string Codigo, string Nombre, int Cantidad);

public sealed record KitDto(int Id, string Codigo, string Nombre, string Descripcion, bool Activo, IReadOnlyList<ComponenteKitDto> Componentes);

public sealed record ClienteDto(int Id, string Codigo, string Nombre, string Identificacion, string Categoria, string Email, string Telefono, string Canal, bool Activo);

public sealed record MovimientoDto(long Id, DateTime FechaUtc, string Sku, string Producto, int Cantidad, string Motivo, string Referencia, string Usuario, int StockResultante)
{
    public string Descripcion => $"{Referencia} {Sku} {(Cantidad > 0 ? "+" : "−")}{Math.Abs(Cantidad)}";
}

public sealed record AuditoriaDto(long Id, DateTime FechaUtc, string Categoria, string Evento, string Resultado, string Detalle, string Usuario, string? Correlacion);

public sealed record OutboxDto(long Id, string Tipo, string Referencia, string Estado, int Intentos, string? UltimoError, DateTime CreadoUtc, DateTime? ProcesadoUtc, string Observadores);

public sealed record NotificacionDto(long Id, DateTime FechaUtc, string Canal, string Destinatario, string Asunto, string Mensaje, string Referencia);

/// <summary>Lado de lectura (CQRS) de pedidos: proyecciones sin rastreo de cambios.</summary>
public interface ILecturaPedidos
{
    Task<IReadOnlyList<PedidoResumenDto>> ListarAsync(CancellationToken ct = default);

    Task<PedidoDetalleDto?> ObtenerAsync(int id, CancellationToken ct = default);
}

/// <summary>Lado de lectura de catálogo y clientes.</summary>
public interface ILecturaCatalogo
{
    Task<IReadOnlyList<ProductoDto>> ListarProductosAsync(CancellationToken ct = default);

    Task<IReadOnlyList<KitDto>> ListarKitsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ClienteDto>> ListarClientesAsync(CancellationToken ct = default);
}

/// <summary>Lado de lectura de bitácoras: movimientos, auditoría, Outbox y notificaciones.</summary>
public interface ILecturaBitacora
{
    Task<IReadOnlyList<MovimientoDto>> ListarMovimientosAsync(int maximo, CancellationToken ct = default);

    Task<IReadOnlyList<AuditoriaDto>> ListarAuditoriaAsync(int maximo, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxDto>> ListarOutboxAsync(CancellationToken ct = default);

    Task<IReadOnlyList<NotificacionDto>> ListarNotificacionesAsync(int maximo, CancellationToken ct = default);
}
