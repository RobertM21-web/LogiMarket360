using LogiMarket.Domain.Catalogo.Composite;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Promociones;

namespace LogiMarket.Domain.Pedidos.Borrador;

/// <summary>Contrato Prototype: crea una copia profunda independiente del original.</summary>
public interface IPrototipo<out T>
{
    T Clonar();
}

/// <summary>Línea editable del carrito.</summary>
public sealed record LineaBorrador(IElementoCatalogo Elemento, int Cantidad);

/// <summary>
/// Carrito/borrador editable en la interfaz. Es el originador del Memento (deshacer/rehacer) y el
/// prototipo de plantillas de carrito reutilizables.
/// </summary>
public sealed class PedidoBorrador : IPrototipo<PedidoBorrador>
{
    private List<LineaBorrador> _lineas = new();

    /// <summary>Id del pedido BORRADOR persistido que se está editando (null si es nuevo).</summary>
    public int? PedidoId { get; private set; }

    public Cliente? Cliente { get; private set; }

    public TipoEntrega Entrega { get; private set; } = TipoEntrega.Estandar;

    public bool Bloqueado { get; private set; }

    public IReadOnlyList<LineaBorrador> Lineas => _lineas.AsReadOnly();

    public bool EstaVacio => _lineas.Count == 0;

    public static PedidoBorrador ParaEditar(Pedido pedido, IReadOnlyList<LineaBorrador> lineas)
    {
        var borrador = new PedidoBorrador
        {
            PedidoId = pedido.Id,
            Cliente = pedido.Cliente,
            Entrega = pedido.TipoEntrega,
            _lineas = lineas.ToList(),
        };
        return borrador;
    }

    public void SeleccionarCliente(Cliente cliente)
    {
        VerificarEditable();
        Cliente = cliente;
    }

    public void CambiarEntrega(TipoEntrega entrega)
    {
        VerificarEditable();
        Entrega = entrega;
    }

    public void AgregarLinea(IElementoCatalogo elemento, int cantidad)
    {
        VerificarEditable();
        Guardas.Positivo(cantidad, "La cantidad");
        var indice = _lineas.FindIndex(l => l.Elemento.Codigo == elemento.Codigo);
        if (indice >= 0)
        {
            _lineas[indice] = _lineas[indice] with { Cantidad = _lineas[indice].Cantidad + cantidad };
        }
        else
        {
            _lineas.Add(new LineaBorrador(elemento, cantidad));
        }
    }

    public void CambiarCantidad(string codigo, int cantidad)
    {
        VerificarEditable();
        Guardas.Positivo(cantidad, "La cantidad");
        var indice = IndiceDe(codigo);
        _lineas[indice] = _lineas[indice] with { Cantidad = cantidad };
    }

    public void QuitarLinea(string codigo)
    {
        VerificarEditable();
        _lineas.RemoveAt(IndiceDe(codigo));
    }

    /// <summary>Marca el borrador como confirmado: ya no admite ediciones ni restauraciones.</summary>
    public void Bloquear() => Bloqueado = true;

    public void AsociarPedido(int pedidoId) => PedidoId = pedidoId;

    public ResumenPedido? Valorar(CalculadoraPedido calculadora, CatalogoFamiliasEntrega familias)
    {
        if (Cliente is null || EstaVacio)
        {
            return null;
        }

        var tarifa = familias.Obtener(Entrega).CrearTarifa().Calcular();
        return calculadora.Calcular(Cliente.Categoria, _lineas.Select(l => new LineaValoracion(l.Elemento, l.Cantidad)), tarifa);
    }

    /// <summary>Vuelca el contenido del borrador en un <see cref="PedidoBuilder"/>.</summary>
    public PedidoBuilder AplicarA(PedidoBuilder builder)
    {
        if (Cliente is not null)
        {
            builder.ParaCliente(Cliente);
        }

        foreach (var linea in _lineas)
        {
            builder.Agregar(linea.Elemento, linea.Cantidad);
        }

        return builder.ConEntrega(Entrega);
    }

    // ---- Memento -------------------------------------------------------------------------------

    public BorradorMemento CrearMemento(string descripcion) =>
        new(Cliente, Entrega, _lineas.Select(l => l with { Elemento = l.Elemento.Clonar() }).ToList(), descripcion);

    public void Restaurar(BorradorMemento memento)
    {
        VerificarEditable();
        Cliente = memento.Cliente;
        Entrega = memento.Entrega;
        _lineas = memento.Lineas.Select(l => l with { Elemento = l.Elemento.Clonar() }).ToList();
    }

    // ---- Prototype -----------------------------------------------------------------------------

    /// <summary>
    /// Copia profunda para plantilla: clona líneas y estructura anidada de kits; no copia el id del
    /// pedido persistido ni el bloqueo (estado de pago).
    /// </summary>
    public PedidoBorrador Clonar() => new()
    {
        Cliente = Cliente,
        Entrega = Entrega,
        _lineas = _lineas.Select(l => new LineaBorrador(l.Elemento.Clonar(), l.Cantidad)).ToList(),
    };

    private int IndiceDe(string codigo)
    {
        var indice = _lineas.FindIndex(l => l.Elemento.Codigo == codigo);
        return indice >= 0 ? indice : throw new ReglaNegocioException("LINEA_NO_EXISTE", $"No existe la línea {codigo}.");
    }

    private void VerificarEditable()
    {
        if (Bloqueado)
        {
            throw new ReglaNegocioException("BORRADOR_BLOQUEADO", "El borrador ya fue confirmado y no admite cambios.");
        }
    }
}

/// <summary>Memento: estado completo e inmutable del borrador. Opaco para el cuidador.</summary>
public sealed class BorradorMemento
{
    internal BorradorMemento(Cliente? cliente, TipoEntrega entrega, IReadOnlyList<LineaBorrador> lineas, string descripcion)
    {
        Cliente = cliente;
        Entrega = entrega;
        Lineas = lineas;
        Descripcion = descripcion;
    }

    public string Descripcion { get; }

    internal Cliente? Cliente { get; }

    internal TipoEntrega Entrega { get; }

    internal IReadOnlyList<LineaBorrador> Lineas { get; }
}
