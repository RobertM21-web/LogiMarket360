using LogiMarket.Domain.Catalogo.Composite;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Promociones;

namespace LogiMarket.Domain.Pedidos;

/// <summary>
/// Builder: ensambla cliente, líneas simples y kits, forma de entrega e instantáneas, y entrega un
/// agregado <see cref="Pedido"/> válido. Un pedido incompleto no puede construirse.
/// </summary>
public sealed class PedidoBuilder
{
    private readonly CalculadoraPedido _calculadora;
    private readonly CatalogoFamiliasEntrega _familias;
    private readonly List<LineaValoracion> _lineas = new();
    private Cliente? _cliente;
    private TipoEntrega _entrega = TipoEntrega.Estandar;

    public PedidoBuilder(CalculadoraPedido calculadora, CatalogoFamiliasEntrega familias)
    {
        _calculadora = calculadora;
        _familias = familias;
    }

    public PedidoBuilder ParaCliente(Cliente cliente)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        return this;
    }

    public PedidoBuilder Agregar(IElementoCatalogo elemento, int cantidad)
    {
        ArgumentNullException.ThrowIfNull(elemento);
        Guardas.Positivo(cantidad, $"La cantidad de {elemento.Codigo}");
        var indice = _lineas.FindIndex(l => l.Elemento.Codigo == elemento.Codigo);
        if (indice >= 0)
        {
            _lineas[indice] = _lineas[indice] with { Cantidad = _lineas[indice].Cantidad + cantidad };
        }
        else
        {
            _lineas.Add(new LineaValoracion(elemento, cantidad));
        }

        return this;
    }

    public PedidoBuilder ConEntrega(TipoEntrega entrega)
    {
        _entrega = entrega;
        return this;
    }

    /// <summary>Valida la completitud y valora el contenido con la Strategy y la familia de entrega.</summary>
    public ContenidoPedido ConstruirContenido()
    {
        if (_cliente is null)
        {
            throw new ReglaNegocioException("PEDIDO_SIN_CLIENTE", "El pedido requiere un cliente.");
        }

        if (_lineas.Count == 0)
        {
            throw new ReglaNegocioException("PEDIDO_SIN_LINEAS", "El pedido requiere al menos una línea.");
        }

        var tarifa = _familias.Obtener(_entrega).CrearTarifa().Calcular();
        var resumen = _calculadora.Calcular(_cliente.Categoria, _lineas, tarifa);
        return new ContenidoPedido(_cliente, _entrega, _lineas.ToList(), resumen);
    }

    public Pedido Construir(string codigo, string usuario, DateTime ahoraUtc) =>
        new(codigo, ConstruirContenido(), usuario, ahoraUtc);
}
