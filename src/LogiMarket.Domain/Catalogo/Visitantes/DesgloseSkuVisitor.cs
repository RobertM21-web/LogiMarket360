using LogiMarket.Domain.Catalogo.Composite;

namespace LogiMarket.Domain.Catalogo.Visitantes;

/// <summary>Línea del desglose físico por SKU.</summary>
public sealed record LineaDesglose(string Sku, string Nombre, int Cantidad, int Nivel);

/// <summary>Visitor que produce el desglose físico por SKU final, conservando el nivel de anidamiento visitado.</summary>
public sealed class DesgloseSkuVisitor : IVisitanteCatalogo
{
    private readonly List<LineaDesglose> _porSku = new();
    private readonly List<string> _kitsVisitados = new();

    public IReadOnlyList<LineaDesglose> Lineas => _porSku;

    /// <summary>Kits recorridos en orden de visita (evidencia de la visita anidada).</summary>
    public IReadOnlyList<string> KitsVisitados => _kitsVisitados;

    public void VisitarKit(KitCompuesto kit, int multiplicador) => _kitsVisitados.Add(kit.Codigo);

    public void VisitarProducto(ProductoSimple producto, int multiplicador)
    {
        var indice = _porSku.FindIndex(l => l.Sku == producto.Sku);
        if (indice >= 0)
        {
            _porSku[indice] = _porSku[indice] with { Cantidad = _porSku[indice].Cantidad + multiplicador };
        }
        else
        {
            _porSku.Add(new LineaDesglose(producto.Sku, producto.Nombre, multiplicador, _kitsVisitados.Count));
        }
    }
}
