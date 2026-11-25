using LogiMarket.Application.Registros;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>Contexto EF Core sobre SQLite. Es la Unidad de Trabajo real del sistema.</summary>
public class LogiMarketDbContext : DbContext
{
    public LogiMarketDbContext(DbContextOptions<LogiMarketDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Producto> Productos => Set<Producto>();

    public DbSet<Kit> Kits => Set<Kit>();

    public DbSet<KitComponente> KitComponentes => Set<KitComponente>();

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    public DbSet<PedidoDetalle> PedidoDetalles => Set<PedidoDetalle>();

    public DbSet<HistorialEstadoPedido> HistorialEstados => Set<HistorialEstadoPedido>();

    public DbSet<Pago> Pagos => Set<Pago>();

    public DbSet<Despacho> Despachos => Set<Despacho>();

    public DbSet<Devolucion> Devoluciones => Set<Devolucion>();

    public DbSet<MovimientoInventario> Movimientos => Set<MovimientoInventario>();

    public DbSet<EventoOutbox> Outbox => Set<EventoOutbox>();

    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Dinero>().HaveConversion<ConversorDinero>().HavePrecision(18, 2);
        configurationBuilder.Properties<DateTime>().HaveConversion<ConversorUtc>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<ConversorUtcNulable>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LogiMarketDbContext).Assembly);

    private sealed class ConversorDinero() : ValueConverter<Dinero, decimal>(d => d.Valor, v => new Dinero(v));

    /// <summary>SQLite no guarda DateTimeKind: todas las fechas se almacenan y se leen como UTC.</summary>
    private sealed class ConversorUtc() : ValueConverter<DateTime, DateTime>(
        d => d.Kind == DateTimeKind.Utc ? d : d.ToUniversalTime(),
        d => DateTime.SpecifyKind(d, DateTimeKind.Utc));

    private sealed class ConversorUtcNulable() : ValueConverter<DateTime?, DateTime?>(
        d => d.HasValue ? (d.Value.Kind == DateTimeKind.Utc ? d : d.Value.ToUniversalTime()) : d,
        d => d.HasValue ? DateTime.SpecifyKind(d.Value, DateTimeKind.Utc) : d);
}
