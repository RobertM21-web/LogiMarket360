using LogiMarket.Domain.Catalogo.Composite;

namespace LogiMarket.Domain.Catalogo.Visitantes;

/// <summary>Visitor que acumula el peso logístico total (kg) de un elemento y sus componentes anidados.</summary>
public sealed class PesoLogisticoVisitor : IVisitanteCatalogo
{
    public decimal TotalKg { get; private set; }

    public int ProductosVisitados { get; private set; }

    public void VisitarKit(KitCompuesto kit, int multiplicador)
    {
        // Los kits no tienen peso propio: su peso es la suma de sus componentes físicos.
    }

    public void VisitarProducto(ProductoSimple producto, int multiplicador)
    {
        TotalKg += producto.PesoKg * multiplicador;
        ProductosVisitados++;
    }
}
