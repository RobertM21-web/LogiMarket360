using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Seguridad;

namespace LogiMarket.Tests.Soporte;

public sealed class RelojFijo : IReloj
{
    public RelojFijo(DateTime ahoraUtc)
    {
        AhoraUtc = ahoraUtc;
    }

    public DateTime AhoraUtc { get; set; }

    public void Avanzar(TimeSpan tiempo) => AhoraUtc += tiempo;
}

public sealed class AuditoriaEnMemoria : IRegistroAuditoria, IAuditoriaSeguridad, IRepositorioAuditoria
{
    public List<RegistroAuditoria> Registros { get; } = new();

    public Task RegistrarAsync(string categoria, string evento, string detalle, string usuario, string resultado = "OK", string? correlacion = null, CancellationToken ct = default)
    {
        Registros.Add(new RegistroAuditoria(categoria, evento, resultado, detalle, usuario, correlacion, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    public Task RegistrarDenegacionAsync(string usuario, string rol, Operacion operacion, string detalle, CancellationToken ct = default) =>
        RegistrarAsync("SEGURIDAD", $"ROL {rol}: {operacion} DENEGADO", detalle, usuario, "DENEGADO", ct: ct);

    public void Agregar(RegistroAuditoria registro) => Registros.Add(registro);
}

public sealed class ProductosEnMemoria : IRepositorioProductos
{
    private readonly List<Producto> _productos;

    public ProductosEnMemoria(IEnumerable<Producto> productos)
    {
        _productos = productos.ToList();
    }

    public Task<Producto?> ObtenerAsync(int id, CancellationToken ct = default) => Task.FromResult(_productos.FirstOrDefault(p => p.Id == id));

    public Task<Producto?> ObtenerPorSkuAsync(string sku, CancellationToken ct = default) => Task.FromResult(_productos.FirstOrDefault(p => p.Sku == sku));

    public Task<IReadOnlyList<Producto>> ListarAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Producto>>(_productos);

    public Task<IReadOnlyList<Producto>> ListarPorIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var set = ids.ToHashSet();
        return Task.FromResult<IReadOnlyList<Producto>>(_productos.Where(p => set.Contains(p.Id)).ToList());
    }

    public void Agregar(Producto producto) => _productos.Add(producto);
}

/// <summary>Unidad de trabajo nula: cuenta confirmaciones para verificar el comportamiento del llamador.</summary>
public sealed class UnidadDeTrabajoContador : IUnidadDeTrabajo
{
    public int Guardados { get; private set; }

    public int Descartes { get; private set; }

    public Task<T> EnTransaccionAsync<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken ct = default) => operacion(ct);

    public Task GuardarCambiosAsync(CancellationToken ct = default)
    {
        Guardados++;
        return Task.CompletedTask;
    }

    public void DescartarCambios() => Descartes++;
}

public static class Sesiones
{
    public static ISesionActual Con(Rol rol, string usuario = "usuario.demo")
    {
        var sesion = new SesionLocal();
        sesion.Iniciar(new UsuarioSesion(1, usuario, usuario, rol));
        return sesion;
    }
}
