using LogiMarket.Domain.Catalogo.Composite;
using LogiMarket.Domain.Catalogo.Recorrido;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Promociones;

/// <summary>Resultado verificable de la valoración de un pedido.</summary>
public sealed record ResumenPedido(
    Dinero Subtotal,
    string Politica,
    decimal Tasa,
    Dinero Descuento,
    Dinero Tarifa,
    Dinero Total,
    int UnidadesFisicas)
{
    /// <summary>Importe de productos tras el descuento (base del reembolso, sin envío).</summary>
    public Dinero ImporteProductos => Subtotal - Descuento;
}

/// <summary>Línea a valorar: elemento del catálogo y cantidad.</summary>
public sealed record LineaValoracion(IElementoCatalogo Elemento, int Cantidad);

/// <summary>
/// Aplica las fórmulas oficiales: subtotal = Σ precio × cantidad; descuento = redondear(subtotal × tasa);
/// total = subtotal − descuento + tarifa. Usa la Strategy a través de su interfaz.
/// </summary>
public sealed class CalculadoraPedido
{
    private readonly SelectorPoliticaDescuento _selector;

    public CalculadoraPedido(SelectorPoliticaDescuento selector)
    {
        _selector = selector;
    }

    public ResumenPedido Calcular(CategoriaCliente categoria, IEnumerable<LineaValoracion> lineas, Dinero tarifa)
    {
        var lista = lineas.ToList();
        var subtotal = lista.Aggregate(Dinero.Cero, (s, l) => s + l.Elemento.Precio.Por(l.Cantidad)).Redondeado();
        var unidades = lista.Sum(l => ExpansionSku.UnidadesFisicas(l.Elemento, l.Cantidad));
        var politica = _selector.Seleccionar(new ContextoDescuento(categoria, unidades));
        var descuento = subtotal.Por(politica.Tasa);
        var total = (subtotal - descuento + tarifa).Redondeado();
        return new ResumenPedido(subtotal, politica.Codigo, politica.Tasa, descuento, tarifa, total, unidades);
    }
}
