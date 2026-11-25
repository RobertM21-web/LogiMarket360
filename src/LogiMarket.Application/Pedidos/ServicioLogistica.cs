using LogiMarket.Application.Eventos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Entrega;

namespace LogiMarket.Application.Pedidos;

/// <summary>Despacho (con guía de la familia de entrega) y registro de entrega.</summary>
public sealed class ServicioLogistica
{
    private readonly IAutorizador _autorizador;
    private readonly IRepositorioPedidos _pedidos;
    private readonly IRepositorioOutbox _outbox;
    private readonly IRepositorioAuditoria _auditoria;
    private readonly ISecuencias _secuencias;
    private readonly IUnidadDeTrabajo _uow;
    private readonly CatalogoFamiliasEntrega _familias;
    private readonly IReloj _reloj;

    public ServicioLogistica(
        IAutorizador autorizador,
        IRepositorioPedidos pedidos,
        IRepositorioOutbox outbox,
        IRepositorioAuditoria auditoria,
        ISecuencias secuencias,
        IUnidadDeTrabajo uow,
        CatalogoFamiliasEntrega familias,
        IReloj reloj)
    {
        _autorizador = autorizador;
        _pedidos = pedidos;
        _outbox = outbox;
        _auditoria = auditoria;
        _secuencias = secuencias;
        _uow = uow;
        _familias = familias;
        _reloj = reloj;
    }

    public async Task<ResultadoOperacion> DespacharAsync(int pedidoId, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.DespacharPedido, $"Despachar pedido {pedidoId}", ct);
        var usuario = _autorizador.UsuarioActual.NombreUsuario;
        var pedido = await _pedidos.ObtenerAsync(pedidoId, ct) ?? throw new ReglaNegocioException("PEDIDO_NO_EXISTE", "El pedido no existe.");
        if (!pedido.Estado.PermiteDespachar)
        {
            throw new ReglaNegocioException("TRANSICION_INVALIDA", $"No se puede despachar un pedido en estado {pedido.Estado.Nombre}.");
        }

        var fabrica = _familias.Obtener(pedido.TipoEntrega);
        var ahora = _reloj.AhoraUtc;
        var guia = fabrica.CrearConstructorGuia().Construir(pedido.Codigo, await _secuencias.SiguienteNumeroGuiaAsync(ct), ahora, fabrica.CrearPlazo());

        await _uow.EnTransaccionAsync(
            async t =>
            {
                pedido.Despachar(guia, usuario, ahora);
                foreach (var evento in pedido.Eventos)
                {
                    _outbox.Agregar(SerializadorEventos.AOutbox(evento, pedido.Codigo, ahora));
                }

                _auditoria.Agregar(new RegistroAuditoria("NEGOCIO", "PEDIDO DESPACHADO", "OK", $"{pedido.Codigo} guía {guia.Numero}", usuario, pedido.Codigo, ahora));
                await _uow.GuardarCambiosAsync(t);
                return true;
            },
            ct);
        pedido.LimpiarEventos();
        return ResultadoOperacion.Ok($"Pedido {pedido.Codigo} despachado con guía {guia.Numero}.", pedido.Id);
    }

    public async Task<ResultadoOperacion> EntregarAsync(int pedidoId, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.EntregarPedido, $"Entregar pedido {pedidoId}", ct);
        var usuario = _autorizador.UsuarioActual.NombreUsuario;
        var pedido = await _pedidos.ObtenerAsync(pedidoId, ct) ?? throw new ReglaNegocioException("PEDIDO_NO_EXISTE", "El pedido no existe.");
        var ahora = _reloj.AhoraUtc;
        pedido.Entregar(usuario, ahora);
        _auditoria.Agregar(new RegistroAuditoria("NEGOCIO", "PEDIDO ENTREGADO", "OK", pedido.Codigo, usuario, pedido.Codigo, ahora));
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Pedido {pedido.Codigo} entregado.", pedido.Id);
    }
}
