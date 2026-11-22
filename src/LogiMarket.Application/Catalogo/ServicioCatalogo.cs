using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Inventario;

namespace LogiMarket.Application.Catalogo;

public sealed record DatosProducto(string Sku, string Nombre, string Categoria, string Descripcion, decimal Precio, decimal PesoKg, int StockInicial);

public sealed record DatosCliente(string Nombre, string Identificacion, CategoriaCliente Categoria, string Email, string Telefono, CanalNotificacion Canal);

/// <summary>Comandos de catálogo: productos, kits y su composición (con detección de ciclos).</summary>
public sealed class ServicioCatalogo
{
    private readonly IAutorizador _autorizador;
    private readonly IRepositorioProductos _productos;
    private readonly IRepositorioKits _kits;
    private readonly IRepositorioMovimientos _movimientos;
    private readonly IRepositorioAuditoria _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IReloj _reloj;

    public ServicioCatalogo(IAutorizador autorizador, IRepositorioProductos productos, IRepositorioKits kits, IRepositorioMovimientos movimientos, IRepositorioAuditoria auditoria, IUnidadDeTrabajo uow, IReloj reloj)
    {
        _autorizador = autorizador;
        _productos = productos;
        _kits = kits;
        _movimientos = movimientos;
        _auditoria = auditoria;
        _uow = uow;
        _reloj = reloj;
    }

    public async Task<ResultadoOperacion> CrearProductoAsync(DatosProducto datos, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.GestionarCatalogo, $"Crear producto {datos.Sku}", ct);
        var usuario = _autorizador.UsuarioActual.NombreUsuario;
        var sku = Producto.NormalizarSku(datos.Sku);
        if (await _productos.ObtenerPorSkuAsync(sku, ct) is not null || (await _kits.ListarAsync(ct)).Any(k => k.Codigo == sku))
        {
            throw new ReglaNegocioException("SKU_DUPLICADO", $"Ya existe un artículo con el código {sku}.");
        }

        var producto = new Producto(sku, datos.Nombre, datos.Categoria, datos.Descripcion, Dinero.Usd(datos.Precio), datos.PesoKg, 0);
        _productos.Agregar(producto);
        var ahora = _reloj.AhoraUtc;
        if (datos.StockInicial > 0)
        {
            _movimientos.Agregar(producto.Reintegrar(datos.StockInicial, MotivoMovimiento.StockInicial, "ALTA", usuario, ahora));
        }

        _auditoria.Agregar(new RegistroAuditoria("NEGOCIO", "PRODUCTO CREADO", "OK", $"{sku} {datos.Nombre} {producto.Precio}", usuario, sku, ahora));
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Producto {sku} creado.", producto.Id);
    }

    public async Task<ResultadoOperacion> ActualizarProductoAsync(int id, DatosProducto datos, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.GestionarCatalogo, $"Editar producto {id}", ct);
        var producto = await _productos.ObtenerAsync(id, ct) ?? throw new ReglaNegocioException("PRODUCTO_NO_EXISTE", "El producto no existe.");
        producto.ActualizarDatos(datos.Nombre, datos.Categoria, datos.Descripcion, Dinero.Usd(datos.Precio), datos.PesoKg);
        _auditoria.Agregar(new RegistroAuditoria("NEGOCIO", "PRODUCTO EDITADO", "OK", $"{producto.Sku} precio {producto.Precio}", _autorizador.UsuarioActual.NombreUsuario, producto.Sku, _reloj.AhoraUtc));
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Producto {producto.Sku} actualizado.", producto.Id);
    }

    public async Task<ResultadoOperacion> CambiarEstadoProductoAsync(int id, bool activo, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.GestionarCatalogo, $"Activar/desactivar producto {id}", ct);
        var producto = await _productos.ObtenerAsync(id, ct) ?? throw new ReglaNegocioException("PRODUCTO_NO_EXISTE", "El producto no existe.");
        if (activo)
        {
            producto.Activar();
        }
        else
        {
            producto.Desactivar();
        }

        _auditoria.Agregar(new RegistroAuditoria("NEGOCIO", activo ? "PRODUCTO ACTIVADO" : "PRODUCTO DESACTIVADO", "OK", producto.Sku, _autorizador.UsuarioActual.NombreUsuario, producto.Sku, _reloj.AhoraUtc));
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Producto {producto.Sku} {(activo ? "activado" : "desactivado")}.", producto.Id);
    }

    public async Task<ResultadoOperacion> CrearKitAsync(string codigo, string nombre, string descripcion, IReadOnlyList<ComposicionPropuesta> composicion, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.GestionarCatalogo, $"Crear kit {codigo}", ct);
        var kits = await _kits.ListarAsync(ct);
        var normalizado = codigo.Trim().ToUpperInvariant();
        if (kits.Any(k => k.Codigo == normalizado) || await _productos.ObtenerPorSkuAsync(normalizado, ct) is not null)
        {
            throw new ReglaNegocioException("SKU_DUPLICADO", $"Ya existe un artículo con el código {normalizado}.");
        }

        var kit = new Kit(normalizado, nombre, descripcion);
        ArbolCatalogo.RedefinirKit(kit, composicion, await _productos.ListarAsync(ct), kits);
        _kits.Agregar(kit);
        _auditoria.Agregar(new RegistroAuditoria("NEGOCIO", "KIT CREADO", "OK", $"{kit.Codigo} con {composicion.Count} componentes", _autorizador.UsuarioActual.NombreUsuario, kit.Codigo, _reloj.AhoraUtc));
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Kit {kit.Codigo} creado.", kit.Id);
    }

    /// <summary>Guarda la composición del kit; rechaza ciclos directos o indirectos sin modificar nada.</summary>
    public async Task<ResultadoOperacion> GuardarComposicionAsync(int kitId, IReadOnlyList<ComposicionPropuesta> composicion, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.GestionarCatalogo, $"Guardar composición kit {kitId}", ct);
        var kits = await _kits.ListarAsync(ct);
        var kit = kits.FirstOrDefault(k => k.Id == kitId) ?? throw new ReglaNegocioException("KIT_NO_EXISTE", "El kit no existe.");
        ArbolCatalogo.RedefinirKit(kit, composicion, await _productos.ListarAsync(ct), kits);
        _auditoria.Agregar(new RegistroAuditoria("NEGOCIO", "KIT COMPOSICION", "OK", $"{kit.Codigo} con {composicion.Count} componentes", _autorizador.UsuarioActual.NombreUsuario, kit.Codigo, _reloj.AhoraUtc));
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Composición de {kit.Codigo} guardada.", kit.Id);
    }
}

/// <summary>Comandos de clientes.</summary>
public sealed class ServicioClientes
{
    private readonly IAutorizador _autorizador;
    private readonly IRepositorioClientes _clientes;
    private readonly IRepositorioAuditoria _auditoria;
    private readonly ISecuencias _secuencias;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IReloj _reloj;

    public ServicioClientes(IAutorizador autorizador, IRepositorioClientes clientes, IRepositorioAuditoria auditoria, ISecuencias secuencias, IUnidadDeTrabajo uow, IReloj reloj)
    {
        _autorizador = autorizador;
        _clientes = clientes;
        _auditoria = auditoria;
        _secuencias = secuencias;
        _uow = uow;
        _reloj = reloj;
    }

    public async Task<ResultadoOperacion> CrearAsync(DatosCliente d, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.GestionarClientes, $"Crear cliente {d.Nombre}", ct);
        if ((await _clientes.ListarAsync(ct)).Any(c => c.Identificacion == d.Identificacion.Trim()))
        {
            throw new ReglaNegocioException("CLIENTE_DUPLICADO", $"Ya existe un cliente con identificación {d.Identificacion}.");
        }

        var cliente = new Cliente(await _secuencias.SiguienteCodigoClienteAsync(ct), d.Nombre, d.Identificacion, d.Categoria, d.Email, d.Telefono, d.Canal);
        _clientes.Agregar(cliente);
        _auditoria.Agregar(new RegistroAuditoria("NEGOCIO", "CLIENTE CREADO", "OK", $"{cliente.Codigo} {cliente.Nombre} {cliente.Categoria}", _autorizador.UsuarioActual.NombreUsuario, cliente.Codigo, _reloj.AhoraUtc));
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Cliente {cliente.Codigo} creado.", cliente.Id);
    }

    public async Task<ResultadoOperacion> ActualizarAsync(int id, DatosCliente d, bool activo, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.GestionarClientes, $"Editar cliente {id}", ct);
        var cliente = await _clientes.ObtenerAsync(id, ct) ?? throw new ReglaNegocioException("CLIENTE_NO_EXISTE", "El cliente no existe.");
        cliente.Actualizar(d.Nombre, d.Identificacion, d.Categoria, d.Email, d.Telefono, d.Canal);
        if (activo)
        {
            cliente.Activar();
        }
        else
        {
            cliente.Desactivar();
        }

        _auditoria.Agregar(new RegistroAuditoria("NEGOCIO", "CLIENTE EDITADO", "OK", $"{cliente.Codigo} {cliente.Nombre} {cliente.Categoria}", _autorizador.UsuarioActual.NombreUsuario, cliente.Codigo, _reloj.AhoraUtc));
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Cliente {cliente.Codigo} actualizado.", cliente.Id);
    }
}

/// <summary>Ajuste manual de inventario auditado (solo ADMIN).</summary>
public sealed class ServicioInventario
{
    private readonly IAutorizador _autorizador;
    private readonly IRepositorioProductos _productos;
    private readonly IRepositorioMovimientos _movimientos;
    private readonly IRepositorioAuditoria _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IReloj _reloj;

    public ServicioInventario(IAutorizador autorizador, IRepositorioProductos productos, IRepositorioMovimientos movimientos, IRepositorioAuditoria auditoria, IUnidadDeTrabajo uow, IReloj reloj)
    {
        _autorizador = autorizador;
        _productos = productos;
        _movimientos = movimientos;
        _auditoria = auditoria;
        _uow = uow;
        _reloj = reloj;
    }

    public async Task<ResultadoOperacion> AjustarAsync(string sku, int cantidad, string motivo, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.AjustarInventario, $"Ajuste {sku} {cantidad:+#;-#;0}", ct);
        if (cantidad == 0)
        {
            throw new ReglaNegocioException("CANTIDAD_INVALIDA", "El ajuste no puede ser cero.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ReglaNegocioException("CAMPO_REQUERIDO", "Indique el motivo del ajuste.");
        }

        var usuario = _autorizador.UsuarioActual.NombreUsuario;
        var producto = await _productos.ObtenerPorSkuAsync(sku, ct) ?? throw new ReglaNegocioException("PRODUCTO_NO_EXISTE", $"No existe el SKU {sku}.");
        var ahora = _reloj.AhoraUtc;
        var movimiento = cantidad > 0
            ? producto.Reintegrar(cantidad, MotivoMovimiento.AjusteManual, "AJUSTE", usuario, ahora)
            : producto.Descontar(-cantidad, MotivoMovimiento.AjusteManual, "AJUSTE", usuario, ahora);
        _movimientos.Agregar(movimiento);
        _auditoria.Agregar(new RegistroAuditoria("INVENTARIO", "AJUSTE MANUAL", "OK", $"{producto.Sku} {cantidad:+#;-#;0} → {producto.Stock}. Motivo: {motivo.Trim()}", usuario, producto.Sku, ahora));
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Stock de {producto.Sku}: {producto.Stock}.", producto.Id);
    }
}
