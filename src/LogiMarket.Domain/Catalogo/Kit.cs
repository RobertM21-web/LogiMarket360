using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo;

/// <summary>Kit persistido: código, nombre y composición (productos y/o kits). Sin stock propio.</summary>
public class Kit
{
    private readonly List<KitComponente> _componentes = new();

    private Kit()
    {
        Codigo = Nombre = Descripcion = string.Empty;
    }

    public Kit(string codigo, string nombre, string descripcion)
    {
        Codigo = Guardas.Texto(codigo, "Código del kit", 30).ToUpperInvariant();
        Renombrar(nombre, descripcion);
        Activo = true;
    }

    public int Id { get; private set; }

    public string Codigo { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;

    public bool Activo { get; private set; }

    public IReadOnlyCollection<KitComponente> Componentes => _componentes.AsReadOnly();

    public void Renombrar(string nombre, string descripcion)
    {
        Nombre = Guardas.Texto(nombre, "Nombre del kit", 120);
        Descripcion = descripcion?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Reemplaza la composición. Solo debe invocarse a través de <see cref="ArbolCatalogo.RedefinirKit"/>,
    /// que valida ciclos indirectos contra el catálogo completo.
    /// </summary>
    internal void ReemplazarComposicion(IReadOnlyList<ComposicionPropuesta> componentes)
    {
        if (componentes.Count == 0)
        {
            throw new ReglaNegocioException("KIT_VACIO", "Un kit debe tener al menos un componente.");
        }

        _componentes.Clear();
        foreach (var c in componentes)
        {
            _componentes.Add(new KitComponente(this, c.ProductoId, c.KitHijoId, c.Cantidad));
        }
    }
}

/// <summary>Componente propuesto para un kit: exactamente uno de producto o kit hijo.</summary>
public sealed record ComposicionPropuesta(int? ProductoId, int? KitHijoId, int Cantidad)
{
    public static ComposicionPropuesta DeProducto(int productoId, int cantidad) => new(productoId, null, cantidad);

    public static ComposicionPropuesta DeKit(int kitId, int cantidad) => new(null, kitId, cantidad);
}
