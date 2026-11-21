using LogiMarket.Application.Metricas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Eventos;

namespace LogiMarket.Application.Eventos;

/// <summary>
/// Observador de notificación: elige el creador (Factory Method) según el canal preferido del cliente.
/// La selección del canal ocurre aquí, nunca en un formulario.
/// </summary>
public sealed class NotificacionClienteObserver : IObservadorEventos
{
    private readonly IRepositorioClientes _clientes;
    private readonly IReadOnlyDictionary<string, CreadorNotificador> _creadores;
    private readonly IRepositorioNotificaciones _notificaciones;
    private readonly MetricasSesion _metricas;

    public NotificacionClienteObserver(IRepositorioClientes clientes, IEnumerable<CreadorNotificador> creadores, IRepositorioNotificaciones notificaciones, MetricasSesion metricas)
    {
        _metricas = metricas;
        _clientes = clientes;
        _creadores = creadores.ToDictionary(c => c.Canal, StringComparer.OrdinalIgnoreCase);
        _notificaciones = notificaciones;
    }

    public string Nombre => "NOTIFICACION";

    public bool Interesa(IEventoDominio evento) => evento is PedidoPagado or DespachoRegistrado or DevolucionAceptada;

    public async Task ManejarAsync(IEventoDominio evento, CancellationToken ct)
    {
        var (clienteId, asunto, cuerpo, referencia) = evento switch
        {
            PedidoPagado e => (e.ClienteId, $"Pedido {e.CodigoPedido} confirmado", $"Recibimos su pago de USD {e.Total:0.00} (ref. {e.ReferenciaCobro}).", e.CodigoPedido),
            DespachoRegistrado e => (e.ClienteId, $"Pedido {e.CodigoPedido} despachado", $"Guía {e.NumeroGuia}. Entrega estimada {e.FechaEstimadaUtc.ToLocalTime():dd/MM/yyyy}.", e.CodigoPedido),
            DevolucionAceptada e => (e.ClienteId, $"Devolución {e.CodigoDevolucion} aceptada", $"Reembolso de USD {e.MontoReembolso:0.00} por el pedido {e.CodigoPedido}.", e.CodigoDevolucion),
            _ => throw new InvalidOperationException($"Evento no soportado: {evento.Tipo}"),
        };

        var cliente = await _clientes.ObtenerAsync(clienteId, ct)
            ?? throw new InvalidOperationException($"Cliente {clienteId} inexistente para notificar.");
        var canal = cliente.CanalPreferido.ToString().ToUpperInvariant();
        if (!_creadores.TryGetValue(canal, out var creador))
        {
            throw new InvalidOperationException($"No hay creador de notificaciones para el canal {canal}.");
        }

        _notificaciones.Agregar(creador.Avisar(new MensajeAviso(cliente.Destino, asunto, cuerpo, referencia)));
        _metricas.RegistrarNotificacion();
    }
}

/// <summary>Observador de auditoría de negocio: deja constancia del evento procesado.</summary>
public sealed class AuditoriaNegocioObserver : IObservadorEventos
{
    private readonly IRepositorioAuditoria _auditoria;
    private readonly IReloj _reloj;

    public AuditoriaNegocioObserver(IRepositorioAuditoria auditoria, IReloj reloj)
    {
        _auditoria = auditoria;
        _reloj = reloj;
    }

    public string Nombre => "AUDITORIA";

    public bool Interesa(IEventoDominio evento) => true;

    public Task ManejarAsync(IEventoDominio evento, CancellationToken ct)
    {
        var (referencia, usuario) = evento switch
        {
            PedidoPagado e => (e.CodigoPedido, e.Usuario),
            DespachoRegistrado e => (e.CodigoPedido, e.Usuario),
            DevolucionAceptada e => (e.CodigoDevolucion, e.Usuario),
            _ => ("-", "sistema"),
        };
        _auditoria.Agregar(new RegistroAuditoria("OUTBOX", $"EVENTO {evento.Tipo}", "PROCESADO", referencia, usuario, referencia, _reloj.AhoraUtc));
        return Task.CompletedTask;
    }
}

/// <summary>Observador de métricas: actualiza el Singleton de indicadores de sesión.</summary>
public sealed class MetricasObserver : IObservadorEventos
{
    private readonly MetricasSesion _metricas;

    public MetricasObserver(MetricasSesion metricas)
    {
        _metricas = metricas;
    }

    public string Nombre => "METRICAS";

    public bool Interesa(IEventoDominio evento) => evento is PedidoPagado or DespachoRegistrado or DevolucionAceptada;

    public Task ManejarAsync(IEventoDominio evento, CancellationToken ct)
    {
        switch (evento)
        {
            case PedidoPagado e: _metricas.RegistrarPedidoConfirmado(e.Total); break;
            case DespachoRegistrado: _metricas.RegistrarDespacho(); break;
            case DevolucionAceptada e: _metricas.RegistrarDevolucion(e.MontoReembolso); break;
        }

        return Task.CompletedTask;
    }
}
