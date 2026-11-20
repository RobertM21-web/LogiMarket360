using LogiMarket.Domain.Catalogo.Composite;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo;

/// <summary>
/// Construye el Composite de catálogo a partir de las entidades persistidas y protege la composición
/// de kits frente a referencias circulares directas o indirectas.
/// </summary>
public sealed class ArbolCatalogo
{
    private readonly Dictionary<string, IElementoCatalogo> _porCodigo;

    private ArbolCatalogo(Dictionary<string, IElementoCatalogo> porCodigo)
    {
        _porCodigo = porCodigo;
    }

    public IReadOnlyCollection<IElementoCatalogo> Elementos => _porCodigo.Values;

    public static ArbolCatalogo Construir(IEnumerable<Producto> productos, IEnumerable<Kit> kits) =>
        Construir(productos, kits, sustituciones: null);

    public IElementoCatalogo Obtener(string codigo) =>
        _porCodigo.TryGetValue(codigo, out var elemento)
            ? elemento
            : throw new ReglaNegocioException("ARTICULO_NO_EXISTE", $"El artículo {codigo} no existe en el catálogo.");

    public bool Existe(string codigo) => _porCodigo.ContainsKey(codigo);

    /// <summary>
    /// Valida la nueva composición contra el catálogo completo (detección de ciclos) y, si es válida,
    /// la aplica a la entidad <paramref name="kit"/>.
    /// </summary>
    public static void RedefinirKit(Kit kit, IReadOnlyList<ComposicionPropuesta> composicion, IEnumerable<Producto> productos, IEnumerable<Kit> kits)
    {
        ArgumentNullException.ThrowIfNull(kit);
        if (composicion.Any(c => c.KitHijoId.HasValue && c.KitHijoId == kit.Id && kit.Id != 0))
        {
            throw new ReglaNegocioException("KIT_CICLICO", $"El kit {kit.Codigo} no puede contenerse a sí mismo.");
        }

        var listaKits = kits.ToList();
        if (!listaKits.Contains(kit))
        {
            listaKits.Add(kit);
        }

        // Construir el árbol con la composición propuesta: lanza KIT_CICLICO si se formara un ciclo.
        Construir(productos, listaKits, new Dictionary<Kit, IReadOnlyList<ComposicionPropuesta>> { [kit] = composicion });
        kit.ReemplazarComposicion(composicion);
    }

    private static ArbolCatalogo Construir(
        IEnumerable<Producto> productos,
        IEnumerable<Kit> kits,
        IReadOnlyDictionary<Kit, IReadOnlyList<ComposicionPropuesta>>? sustituciones)
    {
        var porCodigo = new Dictionary<string, IElementoCatalogo>(StringComparer.OrdinalIgnoreCase);
        var productosPorId = new Dictionary<int, ProductoSimple>();
        foreach (var producto in productos)
        {
            var hoja = producto.ComoElemento();
            productosPorId[producto.Id] = hoja;
            porCodigo[producto.Sku] = hoja;
        }

        var listaKits = kits.ToList();
        var kitsPorId = listaKits.Where(k => k.Id != 0).ToDictionary(k => k.Id);
        var construidos = new Dictionary<Kit, KitCompuesto>();
        var enConstruccion = new HashSet<Kit>();

        KitCompuesto ConstruirKit(Kit kit)
        {
            if (construidos.TryGetValue(kit, out var listo))
            {
                return listo;
            }

            if (!enConstruccion.Add(kit))
            {
                throw new ReglaNegocioException("KIT_CICLICO", $"La composición del kit {kit.Codigo} forma una referencia circular.");
            }

            var nodo = new KitCompuesto(kit.Id, kit.Codigo, kit.Nombre);
            var composicion = sustituciones is not null && sustituciones.TryGetValue(kit, out var propuesta)
                ? propuesta
                : kit.Componentes.Select(c => new ComposicionPropuesta(c.ProductoId, c.KitHijoId, c.Cantidad)).ToList();

            foreach (var componente in composicion)
            {
                IElementoCatalogo hijo = componente.ProductoId is int productoId
                    ? productosPorId.GetValueOrDefault(productoId)
                        ?? throw new ReglaNegocioException("ARTICULO_NO_EXISTE", $"Producto {productoId} inexistente en el kit {kit.Codigo}.")
                    : ConstruirKit(kitsPorId.GetValueOrDefault(componente.KitHijoId!.Value)
                        ?? throw new ReglaNegocioException("ARTICULO_NO_EXISTE", $"Kit {componente.KitHijoId} inexistente en el kit {kit.Codigo}."));
                nodo.Agregar(hijo, componente.Cantidad);
            }

            enConstruccion.Remove(kit);
            construidos[kit] = nodo;
            return nodo;
        }

        foreach (var kit in listaKits)
        {
            porCodigo[kit.Codigo] = ConstruirKit(kit);
        }

        return new ArbolCatalogo(porCodigo);
    }
}
