using LogiMarket.Domain.Catalogo.Composite;

namespace LogiMarket.Domain.Catalogo.Recorrido;

/// <summary>Iterator propio sobre las unidades físicas finales de un elemento del catálogo.</summary>
public interface IIteradorComponentes
{
    bool HaySiguiente();

    ProductoSimple Siguiente();
}

/// <summary>
/// Recorre en profundidad (orden de definición) un kit anidado y entrega un <see cref="ProductoSimple"/>
/// por cada unidad física requerida. Usa una pila explícita: no expone la colección interna del kit,
/// es determinista y termina porque el Composite impide ciclos.
/// </summary>
public sealed class IteradorComponentesFisicos : IIteradorComponentes
{
    private readonly Stack<(IElementoCatalogo Elemento, int Pendientes)> _pila = new();
    private ProductoSimple? _siguiente;

    public IteradorComponentesFisicos(IElementoCatalogo raiz, int cantidad)
    {
        ArgumentNullException.ThrowIfNull(raiz);
        if (cantidad > 0)
        {
            _pila.Push((raiz, cantidad));
        }
    }

    public bool HaySiguiente()
    {
        _siguiente ??= Avanzar();
        return _siguiente is not null;
    }

    public ProductoSimple Siguiente()
    {
        if (!HaySiguiente())
        {
            throw new InvalidOperationException("El recorrido ya terminó.");
        }

        var actual = _siguiente!;
        _siguiente = null;
        return actual;
    }

    private ProductoSimple? Avanzar()
    {
        while (_pila.Count > 0)
        {
            var (elemento, pendientes) = _pila.Pop();
            if (pendientes > 1)
            {
                _pila.Push((elemento, pendientes - 1));
            }

            if (elemento is ProductoSimple hoja)
            {
                return hoja;
            }

            var kit = (KitCompuesto)elemento;
            foreach (var hijo in kit.ComponentesInternos.Reverse())
            {
                _pila.Push((hijo.Elemento, hijo.Cantidad));
            }
        }

        return null;
    }
}
