using LogiMarket.Application.Devoluciones;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Application.Comandos;

/// <summary>Command: operación de negocio encapsulada con su entrada, ejecutable desde la UI o desde pruebas.</summary>
public interface IComandoNegocio
{
    string Nombre { get; }

    string Entrada { get; }

    Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct);

    /// <summary>
    /// Reacción del comando cuando Application deniega el acceso (por defecto ninguna). Cada comando
    /// decide qué métrica afecta; el invocador no necesita conocer tipos concretos (OCP).
    /// </summary>
    void AlDenegarseAcceso(MetricasSesion metricas)
    {
    }
}

public sealed class ConfirmarPedidoCommand : IComandoNegocio
{
    private readonly ConfirmacionPedidoFacade _facade;
    private readonly ProcesadorOutbox _outbox;
    private readonly int _pedidoId;

    public ConfirmarPedidoCommand(ConfirmacionPedidoFacade facade, ProcesadorOutbox outbox, int pedidoId)
    {
        _facade = facade;
        _outbox = outbox;
        _pedidoId = pedidoId;
    }

    public string Nombre => "ConfirmarPedido";

    public string Entrada => $"pedidoId={_pedidoId}";

    public ResultadoConfirmacion? Detalle { get; private set; }

    public async Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct)
    {
        Detalle = await _facade.ConfirmarAsync(_pedidoId, ct);
        if (Detalle.Exito && !Detalle.EsRepeticion)
        {
            // Postconfirmación: los eventos durables salen del Outbox después de confirmar la transacción.
            await _outbox.ProcesarPendientesAsync(ct);
        }

        return new ResultadoOperacion(Detalle.Exito, Detalle.Codigo, Detalle.Mensaje, _pedidoId);
    }

    /// <summary>La Facade rechaza antes de llegar al Proxy: el intento de cobro denegado se contabiliza aquí.</summary>
    public void AlDenegarseAcceso(MetricasSesion metricas) => metricas.RegistrarCobroDenegado();
}

public sealed class CancelarPedidoCommand : IComandoNegocio
{
    private readonly ServicioPedidos _servicio;
    private readonly int _pedidoId;

    public CancelarPedidoCommand(ServicioPedidos servicio, int pedidoId)
    {
        _servicio = servicio;
        _pedidoId = pedidoId;
    }

    public string Nombre => "CancelarPedido";

    public string Entrada => $"pedidoId={_pedidoId}";

    public Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct) => _servicio.CancelarAsync(_pedidoId, ct);
}

public sealed class DespacharPedidoCommand : IComandoNegocio
{
    private readonly ServicioLogistica _servicio;
    private readonly ProcesadorOutbox _outbox;
    private readonly int _pedidoId;

    public DespacharPedidoCommand(ServicioLogistica servicio, ProcesadorOutbox outbox, int pedidoId)
    {
        _servicio = servicio;
        _outbox = outbox;
        _pedidoId = pedidoId;
    }

    public string Nombre => "DespacharPedido";

    public string Entrada => $"pedidoId={_pedidoId}";

    public async Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct)
    {
        var resultado = await _servicio.DespacharAsync(_pedidoId, ct);
        await _outbox.ProcesarPendientesAsync(ct);
        return resultado;
    }
}

public sealed class ProcesarDevolucionCommand : IComandoNegocio
{
    private readonly ServicioDevoluciones _servicio;
    private readonly ProcesadorOutbox _outbox;
    private readonly SolicitudDevolucion _solicitud;

    public ProcesarDevolucionCommand(ServicioDevoluciones servicio, ProcesadorOutbox outbox, SolicitudDevolucion solicitud)
    {
        _servicio = servicio;
        _outbox = outbox;
        _solicitud = solicitud;
    }

    public string Nombre => "ProcesarDevolucion";

    public string Entrada => $"pedidoId={_solicitud.PedidoId}; motivo={_solicitud.Motivo}; sinUso={_solicitud.ProductoSinUso}";

    public ResultadoDevolucion? Detalle { get; private set; }

    public async Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct)
    {
        Detalle = await _servicio.ProcesarAsync(_solicitud, ct);
        if (Detalle.Exito)
        {
            await _outbox.ProcesarPendientesAsync(ct);
        }

        return new ResultadoOperacion(Detalle.Exito, Detalle.Codigo, Detalle.Mensaje, _solicitud.PedidoId);
    }
}

/// <summary>Crea comandos con sus receptores ya resueltos (la UI no conoce los servicios concretos).</summary>
public sealed class FabricaComandos
{
    private readonly ConfirmacionPedidoFacade _facade;
    private readonly ServicioPedidos _pedidos;
    private readonly ServicioLogistica _logistica;
    private readonly ServicioDevoluciones _devoluciones;
    private readonly ProcesadorOutbox _outbox;

    public FabricaComandos(ConfirmacionPedidoFacade facade, ServicioPedidos pedidos, ServicioLogistica logistica, ServicioDevoluciones devoluciones, ProcesadorOutbox outbox)
    {
        _facade = facade;
        _pedidos = pedidos;
        _logistica = logistica;
        _devoluciones = devoluciones;
        _outbox = outbox;
    }

    public ConfirmarPedidoCommand Confirmar(int pedidoId) => new(_facade, _outbox, pedidoId);

    public CancelarPedidoCommand Cancelar(int pedidoId) => new(_pedidos, pedidoId);

    public DespacharPedidoCommand Despachar(int pedidoId) => new(_logistica, _outbox, pedidoId);

    public ProcesarDevolucionCommand Devolver(SolicitudDevolucion solicitud) => new(_devoluciones, _outbox, solicitud);
}

/// <summary>Entrada del historial de invocaciones.</summary>
public sealed record RegistroInvocacion(DateTime FechaUtc, string Usuario, string Comando, string Entrada, bool Exito, string Codigo, string Mensaje);

/// <summary>
/// Invocador: ejecuta cualquier comando, traduce excepciones de negocio/acceso a resultados y registra
/// entrada y resultado (historial en memoria + auditoría persistente).
/// </summary>
public sealed class InvocadorComandos
{
    private readonly object _candado = new();
    private readonly List<RegistroInvocacion> _historial = new();
    private readonly ISesionActual _sesion;
    private readonly IRegistroAuditoria _auditoria;
    private readonly MetricasSesion _metricas;
    private readonly IReloj _reloj;

    public InvocadorComandos(ISesionActual sesion, IRegistroAuditoria auditoria, MetricasSesion metricas, IReloj reloj)
    {
        _sesion = sesion;
        _auditoria = auditoria;
        _metricas = metricas;
        _reloj = reloj;
    }

    public IReadOnlyList<RegistroInvocacion> Historial
    {
        get
        {
            lock (_candado)
            {
                return _historial.ToList();
            }
        }
    }

    public async Task<ResultadoOperacion> EjecutarAsync(IComandoNegocio comando, CancellationToken ct = default)
    {
        ResultadoOperacion resultado;
        try
        {
            resultado = await comando.EjecutarAsync(ct);
        }
        catch (ReglaNegocioException ex)
        {
            resultado = ResultadoOperacion.Error(ex.Codigo, ex.Message);
        }
        catch (AccesoDenegadoException ex)
        {
            resultado = ResultadoOperacion.Error(AccesoDenegadoException.Codigo, ex.Message);
            comando.AlDenegarseAcceso(_metricas);
        }

        var usuario = _sesion.Usuario?.NombreUsuario ?? "anónimo";
        var registro = new RegistroInvocacion(_reloj.AhoraUtc, usuario, comando.Nombre, comando.Entrada, resultado.Exito, resultado.Codigo, resultado.Mensaje);
        lock (_candado)
        {
            _historial.Add(registro);
        }

        _metricas.RegistrarComando();
        await _auditoria.RegistrarAsync("COMANDO", comando.Nombre, $"{comando.Entrada} → {resultado.Mensaje}", usuario, resultado.Exito ? "OK" : resultado.Codigo, ct: ct);
        return resultado;
    }
}
