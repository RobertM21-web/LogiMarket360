using LogiMarket.Domain.Catalogo.Composite;

namespace LogiMarket.Domain.Catalogo.Recorrido;

/// <summary>Cantidad requerida de un SKU final.</summary>
public sealed record RequerimientoSku(ProductoSimple Producto, int Cantidad);

/// <summary>Expande un elemento a sus SKU finales agregando cantidades (usado por inventario y promociones).</summary>
public static class ExpansionSku
{
    public static IReadOnlyList<RequerimientoSku> Expandir(IElementoCatalogo elemento, int cantidad)
    {
        var orden = new List<string>();
        var acumulado = new Dictionary<string, (ProductoSimple Producto, int Cantidad)>(StringComparer.OrdinalIgnoreCase);
        var iterador = elemento.CrearIterador(cantidad);
        while (iterador.HaySiguiente())
        {
            var unidad = iterador.Siguiente();
            if (acumulado.TryGetValue(unidad.Sku, out var previo))
            {
                acumulado[unidad.Sku] = (previo.Producto, previo.Cantidad + 1);
            }
            else
            {
                orden.Add(unidad.Sku);
                acumulado[unidad.Sku] = (unidad, 1);
            }
        }

        return orden.Select(sku => new RequerimientoSku(acumulado[sku].Producto, acumulado[sku].Cantidad)).ToList();
    }

    public static int UnidadesFisicas(IElementoCatalogo elemento, int cantidad) =>
        Expandir(elemento, cantidad).Sum(r => r.Cantidad);
}
