using LogiMarket.Domain.Catalogo.Recorrido;
using LogiMarket.Domain.Catalogo.Visitantes;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo.Composite;

/// <summary>Nodo del Composite: un kit que agrupa productos u otros kits con su cantidad.</summary>
public sealed class KitCompuesto : IElementoCatalogo
{
    private readonly List<ComponenteKit> _componentes = new();

    public KitCompuesto(int kitId, string codigo, string nombre)
    {
        KitId = kitId;
        Codigo = codigo;
        Nombre = nombre;
    }

    public int KitId { get; }

    public string Codigo { get; }

    public string Nombre { get; }

    public bool EsKit => true;

    public Dinero Precio => _componentes.Aggregate(Dinero.Cero, (total, c) => total + c.Elemento.Precio.Por(c.Cantidad));

    /// <summary>Vista de solo lectura de los hijos directos (para mostrar el árbol).</summary>
    public IReadOnlyList<ComponenteKit> Componentes => _componentes.AsReadOnly();

    internal IEnumerable<ComponenteKit> ComponentesInternos => _componentes;

    /// <summary>Agrega un hijo rechazando referencias circulares directas o indirectas.</summary>
    public KitCompuesto Agregar(IElementoCatalogo elemento, int cantidad)
    {
        ArgumentNullException.ThrowIfNull(elemento);
        Guardas.Positivo(cantidad, "La cantidad del componente");
        if (elemento.Contiene(Codigo))
        {
            throw new ReglaNegocioException(
                "KIT_CICLICO",
                $"El kit {Codigo} no puede contener a {elemento.Codigo}: se formaría una referencia circular.");
        }

        var existente = _componentes.FindIndex(c => c.Elemento.Codigo == elemento.Codigo);
        if (existente >= 0)
        {
            _componentes[existente] = _componentes[existente] with { Cantidad = _componentes[existente].Cantidad + cantidad };
        }
        else
        {
            _componentes.Add(new ComponenteKit(elemento, cantidad));
        }

        return this;
    }

    public bool Contiene(string codigo) =>
        string.Equals(Codigo, codigo, StringComparison.OrdinalIgnoreCase)
        || _componentes.Any(c => c.Elemento.Contiene(codigo));

    public IIteradorComponentes CrearIterador(int cantidad = 1) => new IteradorComponentesFisicos(this, cantidad);

    public void Aceptar(IVisitanteCatalogo visitante, int multiplicador = 1)
    {
        visitante.VisitarKit(this, multiplicador);
        foreach (var componente in _componentes)
        {
            componente.Elemento.Aceptar(visitante, multiplicador * componente.Cantidad);
        }
    }

    public IElementoCatalogo Clonar()
    {
        var copia = new KitCompuesto(KitId, Codigo, Nombre);
        foreach (var componente in _componentes)
        {
            copia._componentes.Add(new ComponenteKit(componente.Elemento.Clonar(), componente.Cantidad));
        }

        return copia;
    }

    public override string ToString() => $"{Codigo} - {Nombre}";
}

/// <summary>Hijo de un kit con la cantidad requerida por unidad del kit.</summary>
public sealed record ComponenteKit(IElementoCatalogo Elemento, int Cantidad);
