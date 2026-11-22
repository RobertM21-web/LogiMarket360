using System.Collections.Concurrent;

namespace LogiMarket.Application.Catalogo;

/// <summary>Estado intrínseco compartido (inmutable) de un SKU para la vista.</summary>
public sealed class MetadatosSku
{
    internal MetadatosSku(string sku, string nombre, string categoria, string descripcion)
    {
        Sku = sku;
        Nombre = nombre;
        Categoria = categoria;
        Descripcion = descripcion;
    }

    public string Sku { get; }

    public string Nombre { get; }

    public string Categoria { get; }

    public string Descripcion { get; }
}

/// <summary>Renglón de vista: referencia al flyweight + estado extrínseco propio de la fila.</summary>
public sealed record FilaVistaCatalogo(MetadatosSku Metadatos, int Stock, int Cantidad, decimal PrecioVenta)
{
    public string Sku => Metadatos.Sku;

    public string Nombre => Metadatos.Nombre;

    public string Categoria => Metadatos.Categoria;

    public decimal Importe => Cantidad * PrecioVenta;
}

/// <summary>
/// Flyweight: fábrica que comparte una única instancia de <see cref="MetadatosSku"/> por SKU entre
/// todos los renglones de una vista grande (≥ 1.000 filas).
/// </summary>
public sealed class CatalogoVisualFlyweightFactory
{
    private readonly ConcurrentDictionary<string, MetadatosSku> _cache = new(StringComparer.OrdinalIgnoreCase);

    public int InstanciasCreadas => _cache.Count;

    public MetadatosSku Obtener(string sku, string nombre, string categoria, string descripcion) =>
        _cache.GetOrAdd(sku, s => new MetadatosSku(s, nombre, categoria, descripcion));

    /// <summary>Genera <paramref name="filas"/> renglones repartidos cíclicamente entre los productos dados.</summary>
    public IReadOnlyList<FilaVistaCatalogo> ConstruirVista(IReadOnlyList<Consultas.ProductoDto> productos, int filas)
    {
        if (productos.Count == 0)
        {
            return Array.Empty<FilaVistaCatalogo>();
        }

        var resultado = new List<FilaVistaCatalogo>(filas);
        for (var i = 0; i < filas; i++)
        {
            var p = productos[i % productos.Count];
            var meta = Obtener(p.Sku, p.Nombre, p.Categoria, p.Descripcion);
            resultado.Add(new FilaVistaCatalogo(meta, p.Stock, (i % 7) + 1, p.Precio));
        }

        return resultado;
    }
}
