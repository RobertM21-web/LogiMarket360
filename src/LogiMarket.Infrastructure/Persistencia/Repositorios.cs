using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>Adaptadores Repository sobre EF Core. Los casos de uso solo conocen las interfaces.</summary>
public sealed class RepositorioProductosEf(LogiMarketDbContext db) : IRepositorioProductos
{
    public Task<Producto?> ObtenerAsync(int id, CancellationToken ct = default) => db.Productos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Producto?> ObtenerPorSkuAsync(string sku, CancellationToken ct = default)
    {
        var normalizado = sku.Trim().ToUpperInvariant();
        return db.Productos.FirstOrDefaultAsync(p => p.Sku == normalizado, ct);
    }

    public async Task<IReadOnlyList<Producto>> ListarAsync(CancellationToken ct = default) =>
        await db.Productos.OrderBy(p => p.Sku).ToListAsync(ct);

    public async Task<IReadOnlyList<Producto>> ListarPorIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var lista = ids.Distinct().ToList();
        return await db.Productos.Where(p => lista.Contains(p.Id)).ToListAsync(ct);
    }

    public void Agregar(Producto producto) => db.Productos.Add(producto);
}

public sealed class RepositorioKitsEf(LogiMarketDbContext db) : IRepositorioKits
{
    public Task<Kit?> ObtenerAsync(int id, CancellationToken ct = default) =>
        db.Kits.Include(k => k.Componentes).FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task<IReadOnlyList<Kit>> ListarAsync(CancellationToken ct = default) =>
        await db.Kits.Include(k => k.Componentes).OrderBy(k => k.Codigo).ToListAsync(ct);

    public void Agregar(Kit kit) => db.Kits.Add(kit);
}

public sealed class RepositorioClientesEf(LogiMarketDbContext db) : IRepositorioClientes
{
    public Task<Cliente?> ObtenerAsync(int id, CancellationToken ct = default) => db.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct = default) =>
        await db.Clientes.OrderBy(c => c.Codigo).ToListAsync(ct);

    public void Agregar(Cliente cliente) => db.Clientes.Add(cliente);
}

public sealed class RepositorioPedidosEf(LogiMarketDbContext db) : IRepositorioPedidos
{
    public Task<Pedido?> ObtenerAsync(int id, CancellationToken ct = default) =>
        db.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Detalles).ThenInclude(d => d.Componentes)
            .Include(p => p.Historial)
            .Include(p => p.Pago)
            .Include(p => p.Despacho)
            .Include(p => p.Devolucion)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Pago?> BuscarPagoPorClaveAsync(string claveIdempotencia, CancellationToken ct = default) =>
        db.Pagos.AsNoTracking().FirstOrDefaultAsync(p => p.ClaveIdempotencia == claveIdempotencia, ct);

    public void Agregar(Pedido pedido) => db.Pedidos.Add(pedido);
}

public sealed class RepositorioUsuariosEf(LogiMarketDbContext db) : IRepositorioUsuarios
{
    public Task<Usuario?> BuscarPorNombreAsync(string nombreUsuario, CancellationToken ct = default) =>
        db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario, ct);
}

public sealed class RepositorioMovimientosEf(LogiMarketDbContext db) : IRepositorioMovimientos
{
    public void Agregar(MovimientoInventario movimiento) => db.Movimientos.Add(movimiento);
}

public sealed class RepositorioOutboxEf(LogiMarketDbContext db) : IRepositorioOutbox
{
    public void Agregar(EventoOutbox evento) => db.Outbox.Add(evento);

    public async Task<IReadOnlyList<EventoOutbox>> ListarPendientesAsync(int maximo, CancellationToken ct = default) =>
        await db.Outbox.Where(e => e.Estado == EstadoOutbox.Pendiente).OrderBy(e => e.Id).Take(maximo).ToListAsync(ct);
}

public sealed class RepositorioAuditoriaEf(LogiMarketDbContext db) : IRepositorioAuditoria
{
    public void Agregar(RegistroAuditoria registro) => db.Auditoria.Add(registro);
}

public sealed class RepositorioNotificacionesEf(LogiMarketDbContext db) : IRepositorioNotificaciones
{
    public void Agregar(Notificacion notificacion) => db.Notificaciones.Add(notificacion);
}

/// <summary>Códigos legibles secuenciales; la unicidad final la garantizan los índices UNIQUE.</summary>
public sealed class SecuenciasEf(LogiMarketDbContext db) : ISecuencias
{
    public async Task<string> SiguienteCodigoPedidoAsync(CancellationToken ct = default) =>
        $"PED-{await SiguienteAsync(db.Pedidos.Select(p => p.Codigo), ct):000}";

    public async Task<string> SiguienteCodigoDevolucionAsync(CancellationToken ct = default) =>
        $"DEV-{await SiguienteAsync(db.Devoluciones.Select(d => d.Codigo), ct):000}";

    public async Task<string> SiguienteCodigoClienteAsync(CancellationToken ct = default) =>
        $"C{await SiguienteAsync(db.Clientes.Select(c => c.Codigo), ct):000}";

    public Task<int> SiguienteNumeroGuiaAsync(CancellationToken ct = default) =>
        SiguienteAsync(db.Despachos.Select(d => d.NumeroGuia), ct);

    private static async Task<int> SiguienteAsync(IQueryable<string> codigos, CancellationToken ct)
    {
        var existentes = await codigos.ToListAsync(ct);
        var maximo = existentes
            .Select(c => new string(c.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray()))
            .Select(n => int.TryParse(n, out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return maximo + 1;
    }
}
