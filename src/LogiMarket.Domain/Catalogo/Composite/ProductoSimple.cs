using LogiMarket.Domain.Catalogo.Recorrido;
using LogiMarket.Domain.Catalogo.Visitantes;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo.Composite;

/// <summary>Hoja del Composite: un SKU físico con sus datos de catálogo en el momento de la consulta.</summary>
public sealed class ProductoSimple : IElementoCatalogo
{
    public ProductoSimple(int productoId, string sku, string nombre, string categoria, Dinero precio, decimal pesoKg)
    {
        ProductoId = productoId;
        Codigo = sku;
        Nombre = nombre;
        Categoria = categoria;
        Precio = precio;
        PesoKg = pesoKg;
    }

    public int ProductoId { get; }

    public string Codigo { get; }

    public string Sku => Codigo;

    public string Nombre { get; }

    public string Categoria { get; }

    public bool EsKit => false;

    public Dinero Precio { get; }

    public decimal PesoKg { get; }

    public bool Contiene(string codigo) => string.Equals(Codigo, codigo, StringComparison.OrdinalIgnoreCase);

    public IIteradorComponentes CrearIterador(int cantidad = 1) => new IteradorComponentesFisicos(this, cantidad);

    public void Aceptar(IVisitanteCatalogo visitante, int multiplicador = 1) => visitante.VisitarProducto(this, multiplicador);

    public IElementoCatalogo Clonar() => new ProductoSimple(ProductoId, Codigo, Nombre, Categoria, Precio, PesoKg);

    public override string ToString() => $"{Codigo} - {Nombre}";
}
