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

    internal bool Coincide(string nombre, string categoria, string descripcion) =>
        Nombre == nombre && Categoria == categoria && Descripcion == descripcion;
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

    /// <summary>
    /// Devuelve la instancia compartida del SKU. Si el producto se editó (nombre, categoría o descripción
    /// distintos), la sustituye para que la vista nunca muestre metadatos obsoletos.
    /// </summary>
    public MetadatosSku Obtener(string sku, string nombre, string categoria, string descripcion) =>
        _cache.AddOrUpdate(
            sku,
            static (s, d) => new MetadatosSku(s, d.nombre, d.categoria, d.descripcion),
            static (s, actual, d) => actual.Coincide(d.nombre, d.categoria, d.descripcion) ? actual : new MetadatosSku(s, d.nombre, d.categoria, d.descripcion),
            (nombre, categoria, descripcion)); // lambdas estáticas: sin asignar un cierre por fila

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
