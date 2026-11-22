using LogiMarket.Application.Consultas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Application.Comprobantes;

public enum TipoComprobante
{
    Venta,
    Devolucion,
}

/// <summary>Combina cualquier comprobante con cualquier medio de salida (Bridge) en tiempo de ejecución.</summary>
public sealed class ServicioComprobantes
{
    private readonly IAutorizador _autorizador;
    private readonly ILecturaPedidos _pedidos;
    private readonly IReadOnlyDictionary<string, ISalidaDocumento> _salidas;

    public ServicioComprobantes(IAutorizador autorizador, ILecturaPedidos pedidos, IEnumerable<ISalidaDocumento> salidas)
    {
        _autorizador = autorizador;
        _pedidos = pedidos;
        _salidas = salidas.ToDictionary(s => s.Nombre);
    }

    public IReadOnlyCollection<string> Salidas => _salidas.Keys.ToList();

    public async Task<string> EmitirAsync(int pedidoId, TipoComprobante tipo, string salida, CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, $"Comprobante {tipo} pedido {pedidoId}", ct);
        var pedido = await _pedidos.ObtenerAsync(pedidoId, ct) ?? throw new ReglaNegocioException("PEDIDO_NO_EXISTE", "El pedido no existe.");
        if (pedido.ReferenciaCobro is null)
        {
            throw new ReglaNegocioException("SIN_COBRO", $"El pedido {pedido.Resumen.Codigo} aún no está pagado: no tiene comprobante de venta.");
        }

        if (tipo == TipoComprobante.Devolucion && pedido.Devolucion is null)
        {
            throw new ReglaNegocioException("SIN_DEVOLUCION", $"El pedido {pedido.Resumen.Codigo} no tiene devolución registrada.");
        }

        var medio = _salidas.TryGetValue(salida, out var s) ? s : throw new ArgumentException($"Salida desconocida: {salida}.", nameof(salida));
        Comprobante comprobante = tipo == TipoComprobante.Venta
            ? new ComprobanteVenta(pedido, medio)
            : new ComprobanteDevolucion(pedido, medio);
        return comprobante.Emitir();
    }
}
