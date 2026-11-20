using LogiMarket.Domain.Catalogo.Composite;

namespace LogiMarket.Domain.Catalogo.Visitantes;

/// <summary>
/// Visitor: operaciones de reporte sobre el Composite sin añadirlas a <see cref="ProductoSimple"/>
/// ni a <see cref="KitCompuesto"/>. El multiplicador es la cantidad acumulada desde la raíz.
/// </summary>
public interface IVisitanteCatalogo
{
    void VisitarProducto(ProductoSimple producto, int multiplicador);

    void VisitarKit(KitCompuesto kit, int multiplicador);
}
