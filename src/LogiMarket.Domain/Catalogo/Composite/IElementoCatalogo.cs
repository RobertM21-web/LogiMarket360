using LogiMarket.Domain.Catalogo.Recorrido;
using LogiMarket.Domain.Catalogo.Visitantes;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo.Composite;

/// <summary>
/// Componente del patrón Composite: un producto simple (hoja) o un kit compuesto (nodo),
/// tratados de manera uniforme para precio, recorrido y reportes.
/// </summary>
public interface IElementoCatalogo
{
    string Codigo { get; }

    string Nombre { get; }

    bool EsKit { get; }

    /// <summary>Precio unitario: propio en la hoja, suma recursiva en el kit.</summary>
    Dinero Precio { get; }

    /// <summary>Indica si el elemento es, o contiene directa o indirectamente, el código dado.</summary>
    bool Contiene(string codigo);

    /// <summary>Iterator: recorre las unidades físicas finales (SKU) de <paramref name="cantidad"/> unidades del elemento.</summary>
    IIteradorComponentes CrearIterador(int cantidad = 1);

    /// <summary>Visitor: doble despacho hacia el visitante con el multiplicador de cantidad acumulado.</summary>
    void Aceptar(IVisitanteCatalogo visitante, int multiplicador = 1);

    /// <summary>Copia profunda (usada por el Prototype del carrito).</summary>
    IElementoCatalogo Clonar();
}
