using LogiMarket.Application.Eventos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;

namespace LogiMarket.Application.Demostracion;

public sealed record EscenarioDemostracion(string Proveedor, bool InducirRechazoCobro, bool InducirFalloOutbox, bool ReprocesarPendientes);

/// <summary>
/// Aplica escenarios del modo demostración (solo ADMIN). Usa dobles reales: no altera reglas de negocio
/// ni fabrica resultados; todo queda registrado en la auditoría.
/// </summary>
public sealed class ServicioDemostracion
{
    private readonly IAutorizador _autorizador;
    private readonly IModoDemostracion _modo;
    private readonly ISeleccionProveedorPago _proveedor;
    private readonly ProcesadorOutbox _outbox;
    private readonly IRegistroAuditoria _auditoria;

    public ServicioDemostracion(IAutorizador autorizador, IModoDemostracion modo, ISeleccionProveedorPago proveedor, ProcesadorOutbox outbox, IRegistroAuditoria auditoria)
    {
        _autorizador = autorizador;
        _modo = modo;
        _proveedor = proveedor;
        _outbox = outbox;
        _auditoria = auditoria;
    }

    public EscenarioDemostracion Actual => new(_proveedor.ProveedorActivo, _modo.RechazarCobros, _modo.FallarNotificaciones, false);

    /// <summary>Selección del proveedor desde la pantalla de cobro (VENTAS/ADMIN).</summary>
    public async Task SeleccionarProveedorAsync(string proveedor, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.CobrarPedido, $"Seleccionar proveedor {proveedor}", ct);
        _proveedor.Seleccionar(proveedor);
    }

    public async Task<string> AplicarAsync(EscenarioDemostracion escenario, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.ModoDemostracion, "Aplicar escenario", ct);
        _proveedor.Seleccionar(escenario.Proveedor);
        _modo.RechazarCobros = escenario.InducirRechazoCobro;
        _modo.FallarNotificaciones = escenario.InducirFalloOutbox;
        var detalle = $"Proveedor={escenario.Proveedor}; RechazoCobro={escenario.InducirRechazoCobro}; FalloOutbox={escenario.InducirFalloOutbox}";
        await _auditoria.RegistrarAsync("SISTEMA", "MODO DEMOSTRACION", detalle, _autorizador.UsuarioActual.NombreUsuario, ct: ct);

        if (!escenario.ReprocesarPendientes)
        {
            return $"Escenario aplicado. {detalle}.";
        }

        var resumen = await _outbox.ProcesarPendientesAsync(ct);
        await _auditoria.RegistrarAsync("OUTBOX", "REINTENTO PROCESADO", $"{resumen.Procesados} procesados, {resumen.Fallidos} pendientes", _autorizador.UsuarioActual.NombreUsuario, resumen.Fallidos == 0 ? "OK" : "PENDIENTE", ct: ct);
        return $"Escenario aplicado. Outbox: {resumen.Procesados} procesados, {resumen.Fallidos} pendientes.";
    }
}
