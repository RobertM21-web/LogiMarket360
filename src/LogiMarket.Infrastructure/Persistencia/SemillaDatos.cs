using LogiMarket.Application.Registros;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>
/// Semilla idempotente: inserta solo lo que falta (por SKU, código o usuario). Relanzarla no duplica datos.
/// Todos los datos son ficticios.
/// </summary>
public sealed class SemillaDatos
{
    /// <summary>Cuentas de demostración (contraseñas ficticias documentadas en el README).</summary>
    public static readonly IReadOnlyList<(string Usuario, string Nombre, Rol Rol, string Contrasena)> CuentasDemo =
    [
        ("admin.demo", "Administrador de demostración", Rol.Admin, "Admin#2026"),
        ("ventas.demo", "Vendedor de demostración", Rol.Ventas, "Ventas#2026"),
        ("bodega.demo", "Bodeguero de demostración", Rol.Bodega, "Bodega#2026"),
        ("consulta.demo", "Consulta de demostración", Rol.Consulta, "Consulta#2026"),
    ];

    private static readonly (string Sku, string Nombre, string Categoria, string Descripcion, decimal Precio, int Stock, decimal Peso)[] Productos =
    [
        ("P01", "Mouse USB", "Periféricos", "Mouse óptico USB de 3 botones", 20.00m, 8, 0.20m),
        ("P02", "Teclado", "Periféricos", "Teclado USB distribución latinoamericana", 30.00m, 5, 0.70m),
        ("P03", "Memoria USB", "Almacenamiento", "Memoria USB 3.0 de 32 GB", 10.00m, 20, 0.10m),
        ("P04", "Webcam", "Video", "Cámara web HD 1080p", 40.00m, 4, 0.35m),
        ("P05", "Alfombrilla", "Accesorios", "Alfombrilla de tela antideslizante", 5.00m, 30, 0.10m),
    ];

    private static readonly (string Codigo, string Nombre, string Identificacion, CategoriaCliente Categoria, string Email, string Telefono, CanalNotificacion Canal)[] Clientes =
    [
        ("C001", "Ana López", "1800000001", CategoriaCliente.Vip, "ana.lopez@demo.local", "0990000001", CanalNotificacion.Email),
        ("C002", "Luis Ruiz", "1800000002", CategoriaCliente.Normal, "luis.ruiz@demo.local", "0990000002", CanalNotificacion.Sms),
        ("C003", "Sofía Díaz", "1800000003", CategoriaCliente.Normal, "sofia.diaz@demo.local", "0990000003", CanalNotificacion.Email),
        ("C004", "Carmen Vega", "1800000004", CategoriaCliente.Normal, "carmen.vega@demo.local", "0990000004", CanalNotificacion.Email),
    ];

    private readonly LogiMarketDbContext _db;
    private readonly IHasherContrasenas _hasher;
    private readonly IReloj _reloj;

    public SemillaDatos(LogiMarketDbContext db, IHasherContrasenas hasher, IReloj reloj)
    {
        _db = db;
        _hasher = hasher;
        _reloj = reloj;
    }

    public async Task<int> AplicarAsync(CancellationToken ct = default)
    {
        var insertados = 0;
        var ahora = _reloj.AhoraUtc;

        foreach (var p in Productos)
        {
            if (!await _db.Productos.AnyAsync(x => x.Sku == p.Sku, ct))
            {
                var producto = new Producto(p.Sku, p.Nombre, p.Categoria, p.Descripcion, Dinero.Usd(p.Precio), p.Peso, 0);
                _db.Productos.Add(producto);
                _db.Movimientos.Add(producto.Reintegrar(p.Stock, MotivoMovimiento.StockInicial, "SEMILLA", "sistema", ahora));
                insertados++;
            }
        }

        await _db.SaveChangesAsync(ct);
        insertados += await SembrarKitAsync("KIT_OFICINA", "Kit oficina", "Mouse USB + Teclado", ids => [ComposicionPropuesta.DeProducto(ids["P01"], 1), ComposicionPropuesta.DeProducto(ids["P02"], 1)], ct);
        insertados += await SembrarKitAsync("KIT_TELETRABAJO", "Kit teletrabajo", "Kit oficina + Memoria USB", ids => [ComposicionPropuesta.DeKit(ids["KIT_OFICINA"], 1), ComposicionPropuesta.DeProducto(ids["P03"], 1)], ct);

        foreach (var c in Clientes)
        {
            if (!await _db.Clientes.AnyAsync(x => x.Codigo == c.Codigo, ct))
            {
                _db.Clientes.Add(new Cliente(c.Codigo, c.Nombre, c.Identificacion, c.Categoria, c.Email, c.Telefono, c.Canal));
                insertados++;
            }
        }

        foreach (var u in CuentasDemo)
        {
            if (!await _db.Usuarios.AnyAsync(x => x.NombreUsuario == u.Usuario, ct))
            {
                var (hash, sal) = _hasher.Hashear(u.Contrasena);
                _db.Usuarios.Add(new Usuario(u.Usuario, u.Nombre, u.Rol, hash, sal));
                insertados++;
            }
        }

        if (insertados > 0)
        {
            _db.Auditoria.Add(new RegistroAuditoria("SISTEMA", "SEMILLA APLICADA", "OK", $"{insertados} registros insertados", "sistema", null, ahora));
        }

        await _db.SaveChangesAsync(ct);
        return insertados;
    }

    private async Task<int> SembrarKitAsync(string codigo, string nombre, string descripcion, Func<IReadOnlyDictionary<string, int>, IReadOnlyList<ComposicionPropuesta>> composicion, CancellationToken ct)
    {
        if (await _db.Kits.AnyAsync(k => k.Codigo == codigo, ct))
        {
            return 0;
        }

        var productos = await _db.Productos.ToListAsync(ct);
        var kits = await _db.Kits.Include(k => k.Componentes).ToListAsync(ct);
        var ids = productos.ToDictionary(p => p.Sku, p => p.Id);
        foreach (var k in kits)
        {
            ids[k.Codigo] = k.Id;
        }

        var kit = new Kit(codigo, nombre, descripcion);
        ArbolCatalogo.RedefinirKit(kit, composicion(ids), productos, kits);
        _db.Kits.Add(kit);
        await _db.SaveChangesAsync(ct);
        return 1;
    }
}
