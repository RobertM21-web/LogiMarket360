using LogiMarket.Application.Pedidos.Validacion;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Pedidos.Borrador;
using LogiMarket.Domain.Promociones;

namespace LogiMarket.Application.Pedidos;

public sealed record ResultadoOperacion(bool Exito, string Codigo, string Mensaje, int? Id = null)
{
    public static ResultadoOperacion Ok(string mensaje, int? id = null) => new(true, "OK", mensaje, id);

    public static ResultadoOperacion Error(string codigo, string mensaje, int? id = null) => new(false, codigo, mensaje, id);
}

public sealed record ResultadoValidacionPedido(bool Exito, string Codigo, string Mensaje, int? PedidoId, string? CodigoPedido, IReadOnlyList<string> Recorrido);

/// <summary>Comandos del ciclo comercial del pedido: guardar y validar borradores, y cancelar.</summary>
public sealed class ServicioPedidos
{
    private readonly IAutorizador _autorizador;
    private readonly IRepositorioPedidos _pedidos;
    private readonly IRepositorioClientes _clientes;
    private readonly IRepositorioProductos _productos;
    private readonly IRepositorioKits _kits;
    private readonly IRepositorioAuditoria _auditoria;
    private readonly ISecuencias _secuencias;
    private readonly IUnidadDeTrabajo _uow;
    private readonly CadenaValidacionPedido _validacion;
    private readonly CalculadoraPedido _calculadora;
    private readonly CatalogoFamiliasEntrega _familias;
    private readonly IReloj _reloj;

    public ServicioPedidos(
        IAutorizador autorizador,
        IRepositorioPedidos pedidos,
        IRepositorioClientes clientes,
        IRepositorioProductos productos,
        IRepositorioKits kits,
        IRepositorioAuditoria auditoria,
        ISecuencias secuencias,
        IUnidadDeTrabajo uow,
        CadenaValidacionPedido validacion,
        CalculadoraPedido calculadora,
        CatalogoFamiliasEntrega familias,
        IReloj reloj)
    {
        _autorizador = autorizador;
        _pedidos = pedidos;
        _clientes = clientes;
        _productos = productos;
        _kits = kits;
        _auditoria = auditoria;
        _secuencias = secuencias;
        _uow = uow;
        _validacion = validacion;
        _calculadora = calculadora;
        _familias = familias;
        _reloj = reloj;
    }

    /// <summary>Catálogo actual (activo) como Composite para armar borradores.</summary>
    public async Task<ArbolCatalogo> CargarCatalogoAsync(CancellationToken ct = default) =>
        ArbolCatalogo.Construir(
            (await _productos.ListarAsync(ct)).Where(p => p.Activo),
            (await _kits.ListarAsync(ct)).Where(k => k.Activo));

    /// <summary>Clientes activos (entidades de dominio) para seleccionar en el borrador.</summary>
    public async Task<IReadOnlyList<Cliente>> ClientesActivosAsync(CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Clientes para borrador", ct);
        return (await _clientes.ListarAsync(ct)).Where(c => c.Activo).OrderBy(c => c.Nombre).ToList();
    }

    /// <summary>Valoración en vivo del borrador (subtotal, descuento, entrega, total) sin persistir.</summary>
    public ResumenPedido? Valorar(PedidoBorrador borrador) => borrador.Valorar(_calculadora, _familias);

    /// <summary>Recupera un pedido BORRADOR persistido como borrador editable.</summary>
    public async Task<PedidoBorrador> AbrirBorradorAsync(int pedidoId, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.GestionarBorradores, $"Abrir borrador {pedidoId}", ct);
        var pedido = await _pedidos.ObtenerAsync(pedidoId, ct) ?? throw new ReglaNegocioException("PEDIDO_NO_EXISTE", "El pedido no existe.");
        if (!pedido.Estado.PermiteEditar)
        {
            throw new ReglaNegocioException("TRANSICION_INVALIDA", $"El pedido {pedido.Codigo} está {pedido.Estado.Nombre} y no puede editarse.");
        }

        var arbol = await CargarCatalogoAsync(ct);
        var lineas = pedido.Detalles.Select(d => new LineaBorrador(arbol.Obtener(d.Codigo), d.Cantidad)).ToList();
        return PedidoBorrador.ParaEditar(pedido, lineas);
    }

    /// <summary>
    /// Somete el borrador a la cadena de validación; si es válido, crea (o actualiza) el pedido con su
    /// instantánea y lo pasa a VALIDADO en una transacción.
    /// </summary>
    public async Task<ResultadoValidacionPedido> GuardarYValidarAsync(PedidoBorrador borrador, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.GestionarBorradores, "Validar borrador", ct);
        var usuario = _autorizador.UsuarioActual.NombreUsuario;
        var cliente = borrador.Cliente is null ? null : await _clientes.ObtenerAsync(borrador.Cliente.Id, ct);

        var validacion = await _validacion.ValidarAsync(SolicitudValidacion.DesdeBorrador(borrador) with { Cliente = cliente }, ct);
        if (!validacion.EsValido)
        {
            var r = validacion.Rechazo!;
            return new ResultadoValidacionPedido(false, r.Codigo, $"[{r.Eslabon}] {r.Mensaje}", borrador.PedidoId, null, validacion.Recorrido);
        }

        var builder = new PedidoBuilder(_calculadora, _familias).ParaCliente(cliente!).ConEntrega(borrador.Entrega);
        foreach (var linea in borrador.Lineas)
        {
            builder.Agregar(linea.Elemento, linea.Cantidad);
        }

        var codigoNuevo = borrador.PedidoId is null ? await _secuencias.SiguienteCodigoPedidoAsync(ct) : null;
        var pedido = await _uow.EnTransaccionAsync(
            async t =>
            {
                var ahora = _reloj.AhoraUtc;
                Pedido p;
                if (borrador.PedidoId is int id)
                {
                    p = await _pedidos.ObtenerAsync(id, t) ?? throw new ReglaNegocioException("PEDIDO_NO_EXISTE", "El borrador ya no existe.");
                    p.ReemplazarContenido(builder.ConstruirContenido());
                }
                else
                {
                    p = builder.Construir(codigoNuevo!, usuario, ahora);
                    _pedidos.Agregar(p);
                }

                p.Validar(usuario, ahora);
                _auditoria.Agregar(new RegistroAuditoria("NEGOCIO", "PEDIDO VALIDADO", "OK", $"{p.Codigo} {p.NombreCliente} total {p.Total} ({p.PoliticaDescuento})", usuario, p.Codigo, ahora));
                await _uow.GuardarCambiosAsync(t);
                return p;
            },
            ct);

        borrador.AsociarPedido(pedido.Id);
        return new ResultadoValidacionPedido(true, "VALIDADO", $"Pedido {pedido.Codigo} validado. Total {pedido.Total}.", pedido.Id, pedido.Codigo, validacion.Recorrido);
    }

    /// <summary>Cancela un pedido BORRADOR o VALIDADO (el estado decide; PAGADO se rechaza).</summary>
    public async Task<ResultadoOperacion> CancelarAsync(int pedidoId, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.CancelarPedido, $"Cancelar pedido {pedidoId}", ct);
        var usuario = _autorizador.UsuarioActual.NombreUsuario;
        var pedido = await _pedidos.ObtenerAsync(pedidoId, ct) ?? throw new ReglaNegocioException("PEDIDO_NO_EXISTE", "El pedido no existe.");
        var ahora = _reloj.AhoraUtc;
        pedido.Cancelar(usuario, ahora);
        _auditoria.Agregar(new RegistroAuditoria("NEGOCIO", "PEDIDO CANCELADO", "OK", pedido.Codigo, usuario, pedido.Codigo, ahora));
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Pedido {pedido.Codigo} cancelado.", pedido.Id);
    }
}
