using LogiMarket.Application.Eventos;
using LogiMarket.Application.Pedidos.Validacion;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Pedidos;

/// <summary>Resultado de la confirmación con cobro.</summary>
public sealed record ResultadoConfirmacion(
    bool Exito,
    string Codigo,
    string Mensaje,
    string? CodigoPedido,
    string? ReferenciaCobro,
    decimal Total,
    bool EsRepeticion,
    IReadOnlyList<string> RecorridoValidacion);

/// <summary>
/// Facade: una única operación de confirmación que coordina autorización, validación (Chain), cobro
/// (Proxy → Decorator → Adapter), persistencia de pago + stock + Outbox en una transacción, e idempotencia.
/// No contiene SQL ni reglas de descuento: delega en dominio y puertos.
/// </summary>
public sealed class ConfirmacionPedidoFacade
{
    private readonly IAutorizador _autorizador;
    private readonly IRepositorioPedidos _pedidos;
    private readonly IRepositorioProductos _productos;
    private readonly IRepositorioMovimientos _movimientos;
    private readonly IRepositorioOutbox _outbox;
    private readonly IRepositorioAuditoria _auditoriaTransaccional;
    private readonly IRegistroAuditoria _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly CadenaValidacionPedido _validacion;
    private readonly IPasarelaPago _pasarela;
    private readonly IAnulacionCobro _anulacion;
    private readonly IReloj _reloj;

    public ConfirmacionPedidoFacade(
        IAutorizador autorizador,
        IRepositorioPedidos pedidos,
        IRepositorioProductos productos,
        IRepositorioMovimientos movimientos,
        IRepositorioOutbox outbox,
        IRepositorioAuditoria auditoriaTransaccional,
        IRegistroAuditoria auditoria,
        IUnidadDeTrabajo uow,
        CadenaValidacionPedido validacion,
        IPasarelaPago pasarela,
        IAnulacionCobro anulacion,
        IReloj reloj)
    {
        _autorizador = autorizador;
        _pedidos = pedidos;
        _productos = productos;
        _movimientos = movimientos;
        _outbox = outbox;
        _auditoriaTransaccional = auditoriaTransaccional;
        _auditoria = auditoria;
        _uow = uow;
        _validacion = validacion;
        _pasarela = pasarela;
        _anulacion = anulacion;
        _reloj = reloj;
    }

    public async Task<ResultadoConfirmacion> ConfirmarAsync(int pedidoId, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.CobrarPedido, $"Confirmar pedido {pedidoId}", ct);
        var usuario = _autorizador.UsuarioActual.NombreUsuario;
        var pedido = await _pedidos.ObtenerAsync(pedidoId, ct)
            ?? throw new ReglaNegocioException("PEDIDO_NO_EXISTE", $"El pedido {pedidoId} no existe.");

        // 1. Idempotencia: la misma clave devuelve el resultado original sin volver a cobrar.
        var previo = await _pedidos.BuscarPagoPorClaveAsync(pedido.ClaveIdempotencia, ct);
        if (previo is not null)
        {
            return Repeticion(pedido, previo);
        }

        // 2. Validación encadenada (cliente → líneas → stock → autorización/estado).
        var validacion = await _validacion.ValidarAsync(SolicitudValidacion.DesdePedido(pedido, paraConfirmar: true), ct);
        if (!validacion.EsValido)
        {
            var r = validacion.Rechazo!;
            await _auditoria.RegistrarAsync("NEGOCIO", "CONFIRMACION RECHAZADA", $"{pedido.Codigo} [{r.Eslabon}] {r.Mensaje}", usuario, "RECHAZADO", pedido.Codigo, ct);
            return Fallo(pedido, r.Codigo, r.Mensaje, validacion.Recorrido);
        }

        // 3. Cobro simulado a través del puerto (Proxy → Decorator → Adapter).
        var cobro = await _pasarela.CobrarAsync(pedido.Total.Valor, pedido.ClaveIdempotencia, ct);
        if (!cobro.Aprobado)
        {
            await _auditoria.RegistrarAsync("PAGO", "COBRO RECHAZADO", $"{pedido.Codigo}: {cobro.Mensaje}", usuario, "RECHAZADO", pedido.Codigo, ct);
            return Fallo(pedido, "COBRO_RECHAZADO", $"El proveedor {cobro.Proveedor} rechazó el cobro: {cobro.Mensaje}", validacion.Recorrido);
        }

        // 4. Pedido + pago + stock + Outbox en una sola transacción.
        try
        {
            await _uow.EnTransaccionAsync(
                async t =>
                {
                    var ahora = _reloj.AhoraUtc;
                    pedido.RegistrarPago(cobro.Proveedor, cobro.Referencia!, usuario, ahora);
                    await DescontarStockAsync(pedido, usuario, ahora, t);
                    foreach (var evento in pedido.Eventos)
                    {
                        _outbox.Agregar(SerializadorEventos.AOutbox(evento, pedido.Codigo, ahora));
                    }

                    _auditoriaTransaccional.Agregar(new RegistroAuditoria("NEGOCIO", "PEDIDO CONFIRMADO", "OK", $"{pedido.Codigo} total {pedido.Total} ref. {cobro.Referencia}", usuario, pedido.Codigo, ahora));
                    await _uow.GuardarCambiosAsync(t);
                    return true;
                },
                ct);
            pedido.LimpiarEventos();
            return new ResultadoConfirmacion(true, "CONFIRMADO", $"Pedido {pedido.Codigo} pagado. Referencia {cobro.Referencia}.", pedido.Codigo, cobro.Referencia, pedido.Total.Valor, false, validacion.Recorrido);
        }
        catch (Exception ex) when (ex is ConflictoPersistenciaException or ReglaNegocioException or InvalidOperationException)
        {
            // Sin persistencia parcial: se descartan los cambios y se compensa el cobro simulado aprobado.
            _uow.DescartarCambios();
            await _anulacion.AnularAsync(cobro.Referencia!, ex.Message, ct);
            await _auditoria.RegistrarAsync("PAGO", "COBRO ANULADO", $"{pedido.Codigo} ref. {cobro.Referencia}: {ex.Message}", usuario, "COMPENSADO", pedido.Codigo, ct);

            var ganador = await _pedidos.BuscarPagoPorClaveAsync(pedido.ClaveIdempotencia, ct);
            if (ganador is not null)
            {
                return Repeticion(pedido, ganador);
            }

            var codigo = ex is ReglaNegocioException rn ? rn.Codigo : ex is ConflictoPersistenciaException cp ? $"CONFLICTO_{cp.Tipo}" : "ERROR_PERSISTENCIA";
            return Fallo(pedido, codigo, $"No se confirmó el pedido {pedido.Codigo}: {ex.Message}", validacion.Recorrido);
        }
    }

    private async Task DescontarStockAsync(Pedido pedido, string usuario, DateTime ahora, CancellationToken ct)
    {
        var requerimientos = pedido.RequerimientosStock();
        var productos = (await _productos.ListarPorIdsAsync(requerimientos.Select(r => r.ProductoId), ct)).ToDictionary(p => p.Id);
        foreach (var req in requerimientos)
        {
            var producto = productos.GetValueOrDefault(req.ProductoId)
                ?? throw new ReglaNegocioException("PRODUCTO_NO_DISPONIBLE", $"El producto {req.Sku} ya no existe.");
            _movimientos.Agregar(producto.Descontar(req.Cantidad, MotivoMovimiento.Venta, pedido.Codigo, usuario, ahora));
        }
    }

    private static ResultadoConfirmacion Repeticion(Pedido pedido, Pago pago) =>
        new(true, "YA_CONFIRMADO", $"El pedido {pedido.Codigo} ya estaba pagado (ref. {pago.Referencia}). No se cobró de nuevo.", pedido.Codigo, pago.Referencia, pago.Monto.Valor, true, Array.Empty<string>());

    private static ResultadoConfirmacion Fallo(Pedido pedido, string codigo, string mensaje, IReadOnlyList<string> recorrido) =>
        new(false, codigo, mensaje, pedido.Codigo, null, pedido.Total.Valor, false, recorrido);
}
