using LogiMarket.Application.Registros;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Seguridad;

namespace LogiMarket.Application.Puertos;

/// <summary>
/// Unidad de trabajo: confirma en una sola transacción todos los cambios rastreados por los repositorios.
/// </summary>
public interface IUnidadDeTrabajo
{
    /// <summary>Ejecuta la operación dentro de una transacción; confirma solo si no hay excepción.</summary>
    Task<T> EnTransaccionAsync<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken ct = default);

    Task GuardarCambiosAsync(CancellationToken ct = default);

    /// <summary>Descarta los cambios rastreados tras una falla para no contaminar operaciones siguientes.</summary>
    void DescartarCambios();
}

/// <summary>Conflicto de concurrencia optimista o violación de unicidad al confirmar.</summary>
public sealed class ConflictoPersistenciaException : Exception
{
    public ConflictoPersistenciaException(string tipo, string mensaje, Exception? interna = null)
        : base(mensaje, interna)
    {
        Tipo = tipo;
    }

    /// <summary>CONCURRENCIA o UNICIDAD.</summary>
    public string Tipo { get; }
}

public interface IRepositorioProductos
{
    Task<Producto?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<Producto?> ObtenerPorSkuAsync(string sku, CancellationToken ct = default);

    Task<IReadOnlyList<Producto>> ListarAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Producto>> ListarPorIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);

    void Agregar(Producto producto);
}

public interface IRepositorioKits
{
    Task<Kit?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Kit>> ListarAsync(CancellationToken ct = default);

    void Agregar(Kit kit);
}

public interface IRepositorioClientes
{
    Task<Cliente?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct = default);

    void Agregar(Cliente cliente);
}

public interface IRepositorioPedidos
{
    /// <summary>Carga el agregado completo (detalles, componentes, historial, pago, despacho, devolución, cliente).</summary>
    Task<Pedido?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<Pago?> BuscarPagoPorClaveAsync(string claveIdempotencia, CancellationToken ct = default);

    void Agregar(Pedido pedido);
}

public interface IRepositorioUsuarios
{
    Task<Usuario?> BuscarPorNombreAsync(string nombreUsuario, CancellationToken ct = default);
}

public interface IRepositorioMovimientos
{
    void Agregar(MovimientoInventario movimiento);
}

public interface IRepositorioOutbox
{
    void Agregar(EventoOutbox evento);

    Task<IReadOnlyList<EventoOutbox>> ListarPendientesAsync(int maximo, CancellationToken ct = default);
}

/// <summary>Auditoría dentro de la transacción de negocio (se confirma o se descarta junto con ella).</summary>
public interface IRepositorioAuditoria
{
    void Agregar(RegistroAuditoria registro);
}

/// <summary>Auditoría independiente: se confirma de inmediato aunque la operación de negocio falle.</summary>
public interface IRegistroAuditoria
{
    Task RegistrarAsync(string categoria, string evento, string detalle, string usuario, string resultado = "OK", string? correlacion = null, CancellationToken ct = default);
}

public interface IRepositorioNotificaciones
{
    void Agregar(Notificacion notificacion);
}

/// <summary>Generador de códigos de negocio legibles.</summary>
public interface ISecuencias
{
    Task<string> SiguienteCodigoPedidoAsync(CancellationToken ct = default);

    Task<string> SiguienteCodigoDevolucionAsync(CancellationToken ct = default);

    Task<string> SiguienteCodigoClienteAsync(CancellationToken ct = default);

    Task<int> SiguienteNumeroGuiaAsync(CancellationToken ct = default);
}
