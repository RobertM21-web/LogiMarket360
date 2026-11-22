using System.Globalization;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Puertos;

namespace LogiMarket.Application.Comprobantes;

/// <summary>
/// Abstracción del Bridge: un comprobante sabe qué contenido emitir; el <see cref="ISalidaDocumento"/>
/// (implementador) decide cómo y dónde se transmite. 2 comprobantes × 2 salidas = 4 combinaciones sin
/// clases duplicadas.
/// </summary>
public abstract class Comprobante
{
    protected static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

    protected Comprobante(ISalidaDocumento salida)
    {
        Salida = salida;
    }

    protected ISalidaDocumento Salida { get; }

    public abstract string Titulo { get; }

    /// <summary>Construye el contenido y lo delega al medio de salida.</summary>
    public string Emitir() => Salida.Emitir(Titulo, ConstruirLineas());

    protected abstract IReadOnlyList<string> ConstruirLineas();

    protected static string Usd(decimal valor) => "USD " + valor.ToString("0.00", Cultura);

    protected static IEnumerable<string> Encabezado(string tipo) =>
    [
        "LOGIMARKET 360",
        $"{tipo} — documento interno de demostración (no tributario, sin IVA)",
        new string('-', 60),
    ];
}

/// <summary>Abstracción refinada: comprobante de venta.</summary>
public sealed class ComprobanteVenta : Comprobante
{
    private readonly PedidoDetalleDto _pedido;

    public ComprobanteVenta(PedidoDetalleDto pedido, ISalidaDocumento salida)
        : base(salida)
    {
        _pedido = pedido;
    }

    public override string Titulo => $"Comprobante de venta {_pedido.Resumen.Codigo}";

    protected override IReadOnlyList<string> ConstruirLineas()
    {
        var r = _pedido.Resumen;
        var lineas = Encabezado("COMPROBANTE DE VENTA").ToList();
        lineas.Add($"Pedido: {r.Codigo}    Cliente: {r.Cliente} ({r.Categoria})");
        lineas.Add($"Estado: {r.Estado}    Pago: {_pedido.ReferenciaCobro ?? "pendiente"}");
        lineas.Add(new string('-', 60));
        lineas.Add($"{"Artículo",-24}{"Cant.",6}{"P. unit.",14}{"Importe",14}");
        lineas.AddRange(_pedido.Lineas.Select(l => $"{Truncar(l.Codigo + " " + l.Nombre, 24),-24}{l.Cantidad,6}{Usd(l.PrecioUnitario),14}{Usd(l.Importe),14}"));
        lineas.Add(new string('-', 60));
        lineas.Add($"{"Subtotal",-44}{Usd(r.Subtotal),14}");
        lineas.Add($"{"Descuento " + r.Politica,-44}{"-" + Usd(r.Descuento),14}");
        lineas.Add($"{"Entrega " + r.Entrega,-44}{Usd(r.Tarifa),14}");
        lineas.Add($"{"TOTAL",-44}{Usd(r.Total),14}");
        return lineas;
    }

    private static string Truncar(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}

/// <summary>Abstracción refinada: comprobante de devolución.</summary>
public sealed class ComprobanteDevolucion : Comprobante
{
    private readonly PedidoDetalleDto _pedido;

    public ComprobanteDevolucion(PedidoDetalleDto pedido, ISalidaDocumento salida)
        : base(salida)
    {
        _pedido = pedido.Devolucion is null
            ? throw new InvalidOperationException($"El pedido {pedido.Resumen.Codigo} no tiene devolución.")
            : pedido;
    }

    public override string Titulo => $"Comprobante de devolución {_pedido.Devolucion!.Codigo}";

    protected override IReadOnlyList<string> ConstruirLineas()
    {
        var d = _pedido.Devolucion!;
        var r = _pedido.Resumen;
        var lineas = Encabezado("COMPROBANTE DE DEVOLUCIÓN").ToList();
        lineas.Add($"Devolución: {d.Codigo}    Pedido: {r.Codigo}");
        lineas.Add($"Cliente: {r.Cliente}    Motivo: {d.Motivo}    Días desde la entrega: {d.DiasDesdeEntrega}");
        lineas.Add($"Reintegro de stock: {(d.ReintegraStock ? "SÍ" : "NO")}    Ref. reembolso: {d.ReferenciaReembolso}");
        lineas.Add(new string('-', 60));
        lineas.Add($"{"Subtotal de productos",-44}{Usd(r.Subtotal),14}");
        lineas.Add($"{"Descuento aplicado " + r.Politica,-44}{"-" + Usd(r.Descuento),14}");
        lineas.Add($"{"Tarifa de envío (no reembolsable)",-44}{Usd(r.Tarifa),14}");
        lineas.Add($"{"TOTAL REEMBOLSADO",-44}{Usd(d.Monto),14}");
        return lineas;
    }
}
