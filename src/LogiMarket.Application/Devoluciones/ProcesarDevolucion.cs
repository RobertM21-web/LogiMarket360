using LogiMarket.Application.Eventos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Devoluciones;

/// <summary>Datos capturados para solicitar una devolución total.</summary>
public sealed record SolicitudDevolucion(int PedidoId, MotivoDevolucion Motivo, bool ProductoSinUso, bool ReintegrarStock, string Observacion = "");

public sealed record RechazoDevolucion(string Paso, string Codigo, string Mensaje);

/// <summary>Evaluación previa de la política (para el panel "Validación de política").</summary>
public sealed record EvaluacionDevolucion(
    bool Elegible,
    string Mensaje,
    DateTime? FechaEntregaUtc,
    int DiasTranscurridos,
    int PlazoMaximoDias,
    decimal Reembolso,
    bool ReintegraStock);

public sealed record ResultadoDevolucion(
    bool Exito,
    string Codigo,
    string Mensaje,
    string? CodigoDevolucion,
    decimal Reembolso,
    IReadOnlyList<string> Pasos);

/// <summary>Dependencias compartidas por los procesadores de devolución.</summary>
public sealed record DependenciasDevolucion(
    IAutorizador Autorizador,
    IRepositorioPedidos Pedidos,
    IRepositorioProductos Productos,
    IRepositorioMovimientos Movimientos,
    IRepositorioOutbox Outbox,
    IRepositorioAuditoria AuditoriaTransaccional,
    IRegistroAuditoria Auditoria,
    ISecuencias Secuencias,
    IServicioReembolso Reembolso,
    IUnidadDeTrabajo Uow,
    IReloj Reloj);

/// <summary>
/// Template Method: <see cref="ProcesarAsync"/> fija la secuencia invariante
/// verificar elegibilidad → inspeccionar condición → calcular reembolso → registrar devolución/stock → generar evento.
/// Las variantes (DEFECTO, ARREPENTIMIENTO) solo personalizan los pasos permitidos.
/// </summary>
public abstract class ProcesarDevolucion
{
    private readonly DependenciasDevolucion _d;

    protected ProcesarDevolucion(DependenciasDevolucion dependencias)
    {
        _d = dependencias;
    }

    public abstract MotivoDevolucion Motivo { get; }

    protected abstract int PlazoMaximoDias { get; }

    /// <summary>Método plantilla: secuencia no sobrescribible.</summary>
    public async Task<ResultadoDevolucion> ProcesarAsync(SolicitudDevolucion solicitud, CancellationToken ct = default)
    {
        await _d.Autorizador.VerificarAsync(Operacion.ProcesarDevolucion, $"Devolución pedido {solicitud.PedidoId}", ct);
        var usuario = _d.Autorizador.UsuarioActual.NombreUsuario;
        var pedido = await _d.Pedidos.ObtenerAsync(solicitud.PedidoId, ct)
            ?? throw new ReglaNegocioException("PEDIDO_NO_EXISTE", "El pedido no existe.");
        var ahora = _d.Reloj.AhoraUtc;
        var dias = pedido.DiasDesdeEntrega(ahora);
        var pasos = new List<string>();

        pasos.Add("1. Verificar elegibilidad");
        var rechazo = VerificarElegibilidad(pedido);
        pasos.Add("2. Inspeccionar condición");
        rechazo ??= InspeccionarCondicion(pedido, solicitud, dias);
        if (rechazo is not null)
        {
            await _d.Auditoria.RegistrarAsync("NEGOCIO", "DEVOLUCION RECHAZADA", $"{pedido.Codigo} {Motivo}: {rechazo.Mensaje}", usuario, "RECHAZADO", pedido.Codigo, ct);
            return new ResultadoDevolucion(false, rechazo.Codigo, rechazo.Mensaje, null, 0m, pasos);
        }

        pasos.Add("3. Calcular reembolso");
        var monto = CalcularReembolso(pedido);

        pasos.Add("4. Registrar devolución y stock");
        var codigo = await _d.Secuencias.SiguienteCodigoDevolucionAsync(ct);
        var reembolso = await _d.Reembolso.ReembolsarAsync(monto.Valor, $"REEMB-{pedido.ClaveIdempotencia}", ct);
        if (!reembolso.Aprobado)
        {
            await _d.Auditoria.RegistrarAsync("PAGO", "REEMBOLSO RECHAZADO", $"{pedido.Codigo}: {reembolso.Mensaje}", usuario, "RECHAZADO", pedido.Codigo, ct);
            return new ResultadoDevolucion(false, "REEMBOLSO_RECHAZADO", reembolso.Mensaje, null, 0m, pasos);
        }

        var reintegra = DebeReintegrarStock(solicitud);
        try
        {
            await _d.Uow.EnTransaccionAsync(
                async t =>
                {
                    var devolucion = pedido.RegistrarDevolucion(codigo, Motivo, reintegra, reembolso.Referencia!, solicitud.Observacion, usuario, ahora);
                    if (reintegra)
                    {
                        await ReintegrarStockAsync(pedido, devolucion.Codigo, usuario, ahora, t);
                    }

                    pasos.Add("5. Generar evento");
                    foreach (var evento in pedido.Eventos)
                    {
                        _d.Outbox.Agregar(SerializadorEventos.AOutbox(evento, devolucion.Codigo, ahora));
                    }

                    _d.AuditoriaTransaccional.Agregar(new RegistroAuditoria("NEGOCIO", "DEVOLUCION ACEPTADA", "OK", $"{devolucion.Codigo} pedido {pedido.Codigo} {Motivo} reembolso {monto}", usuario, pedido.Codigo, ahora));
                    await _d.Uow.GuardarCambiosAsync(t);
                    return devolucion;
                },
                ct);
        }
        catch (Exception ex) when (ex is ConflictoPersistenciaException or ReglaNegocioException)
        {
            _d.Uow.DescartarCambios();
            await _d.Auditoria.RegistrarAsync("NEGOCIO", "DEVOLUCION RECHAZADA", $"{pedido.Codigo}: {ex.Message}", usuario, "RECHAZADO", pedido.Codigo, ct);
            var cod = ex is ReglaNegocioException rn ? rn.Codigo : "DEVOLUCION_DUPLICADA";
            return new ResultadoDevolucion(false, cod, ex.Message, null, 0m, pasos);
        }

        pedido.LimpiarEventos();
        return new ResultadoDevolucion(true, "ACEPTADA", $"Devolución {codigo} aceptada. Reembolso {monto} (sin envío).", codigo, monto.Valor, pasos);
    }

    /// <summary>Evalúa la política sin modificar nada (vista previa).</summary>
    public EvaluacionDevolucion Evaluar(Pedido pedido, SolicitudDevolucion solicitud)
    {
        var dias = pedido.DiasDesdeEntrega(_d.Reloj.AhoraUtc);
        var rechazo = VerificarElegibilidad(pedido) ?? InspeccionarCondicion(pedido, solicitud, dias);
        return new EvaluacionDevolucion(
            rechazo is null,
            rechazo?.Mensaje ?? $"Cumple la política {Motivo.ToString().ToUpperInvariant()} (≤ {PlazoMaximoDias} días).",
            pedido.FechaEntregaUtc,
            dias,
            PlazoMaximoDias,
            rechazo is null ? CalcularReembolso(pedido).Valor : 0m,
            rechazo is null && DebeReintegrarStock(solicitud));
    }

    /// <summary>Paso 1 (común): pedido ENTREGADO y no devuelto antes.</summary>
    protected virtual RechazoDevolucion? VerificarElegibilidad(Pedido pedido)
    {
        if (pedido.FueDevuelto)
        {
            return new RechazoDevolucion("Elegibilidad", "DEVOLUCION_DUPLICADA", $"El pedido {pedido.Codigo} ya fue devuelto ({pedido.Devolucion!.Codigo}).");
        }

        return pedido.Estado.PermiteDevolver
            ? null
            : new RechazoDevolucion("Elegibilidad", "PEDIDO_NO_ENTREGADO", $"Solo se devuelven pedidos ENTREGADOS; {pedido.Codigo} está {pedido.Estado.Nombre}.");
    }

    /// <summary>Paso 2 (personalizable): plazo máximo según el motivo.</summary>
    protected virtual RechazoDevolucion? InspeccionarCondicion(Pedido pedido, SolicitudDevolucion solicitud, int diasDesdeEntrega) =>
        diasDesdeEntrega <= PlazoMaximoDias
            ? null
            : new RechazoDevolucion("Condición", "FUERA_DE_PLAZO", $"{Motivo.ToString().ToUpperInvariant()} admite hasta {PlazoMaximoDias} días; han pasado {diasDesdeEntrega}.");

    /// <summary>Paso 3: importe de productos después del descuento, sin la tarifa de envío.</summary>
    protected virtual Dinero CalcularReembolso(Pedido pedido) => pedido.ImporteProductos;

    /// <summary>Gancho: decide si el stock se reintegra.</summary>
    protected abstract bool DebeReintegrarStock(SolicitudDevolucion solicitud);

    private async Task ReintegrarStockAsync(Pedido pedido, string referencia, string usuario, DateTime ahora, CancellationToken ct)
    {
        var requerimientos = pedido.RequerimientosStock();
        var productos = (await _d.Productos.ListarPorIdsAsync(requerimientos.Select(r => r.ProductoId), ct)).ToDictionary(p => p.Id);
        foreach (var req in requerimientos)
        {
            if (productos.TryGetValue(req.ProductoId, out var producto))
            {
                _d.Movimientos.Agregar(producto.Reintegrar(req.Cantidad, MotivoMovimiento.Devolucion, referencia, usuario, ahora));
            }
        }
    }
}

/// <summary>Variante DEFECTO: hasta 30 días; reintegra stock salvo que el operador indique lo contrario (D08).</summary>
public sealed class DevolucionDefecto : ProcesarDevolucion
{
    public DevolucionDefecto(DependenciasDevolucion dependencias)
        : base(dependencias)
    {
    }

    public override MotivoDevolucion Motivo => MotivoDevolucion.Defecto;

    protected override int PlazoMaximoDias => 30;

    protected override bool DebeReintegrarStock(SolicitudDevolucion solicitud) => solicitud.ReintegrarStock;
}

/// <summary>Variante ARREPENTIMIENTO: hasta 7 días y producto sin uso; siempre reintegra stock.</summary>
public sealed class DevolucionArrepentimiento : ProcesarDevolucion
{
    public DevolucionArrepentimiento(DependenciasDevolucion dependencias)
        : base(dependencias)
    {
    }

    public override MotivoDevolucion Motivo => MotivoDevolucion.Arrepentimiento;

    protected override int PlazoMaximoDias => 7;

    protected override RechazoDevolucion? InspeccionarCondicion(Pedido pedido, SolicitudDevolucion solicitud, int diasDesdeEntrega) =>
        base.InspeccionarCondicion(pedido, solicitud, diasDesdeEntrega)
        ?? (solicitud.ProductoSinUso
            ? null
            : new RechazoDevolucion("Condición", "PRODUCTO_USADO", "ARREPENTIMIENTO exige que el producto esté sin uso."));

    protected override bool DebeReintegrarStock(SolicitudDevolucion solicitud) => true;
}

/// <summary>Selecciona la variante del Template Method por motivo y ofrece la evaluación previa.</summary>
public sealed class ServicioDevoluciones
{
    private readonly IReadOnlyDictionary<MotivoDevolucion, ProcesarDevolucion> _procesadores;
    private readonly IRepositorioPedidos _pedidos;

    public ServicioDevoluciones(IEnumerable<ProcesarDevolucion> procesadores, IRepositorioPedidos pedidos)
    {
        _procesadores = procesadores.ToDictionary(p => p.Motivo);
        _pedidos = pedidos;
    }

    public Task<ResultadoDevolucion> ProcesarAsync(SolicitudDevolucion solicitud, CancellationToken ct = default) =>
        _procesadores[solicitud.Motivo].ProcesarAsync(solicitud, ct);

    public async Task<EvaluacionDevolucion> EvaluarAsync(SolicitudDevolucion solicitud, CancellationToken ct = default)
    {
        var pedido = await _pedidos.ObtenerAsync(solicitud.PedidoId, ct) ?? throw new ReglaNegocioException("PEDIDO_NO_EXISTE", "El pedido no existe.");
        return _procesadores[solicitud.Motivo].Evaluar(pedido, solicitud);
    }
}
