using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Entrega;

public enum TipoEntrega
{
    Estandar = 0,
    Expres = 1,
}

/// <summary>Producto abstracto 1: tarifa de la familia.</summary>
public interface ITarifaEntrega
{
    TipoEntrega Familia { get; }

    Dinero Calcular();
}

/// <summary>Producto abstracto 2: plazo de la familia.</summary>
public interface IPlazoEntrega
{
    TipoEntrega Familia { get; }

    int Dias { get; }

    DateTime FechaEstimada(DateTime despachoUtc);
}

/// <summary>Guía de envío simulada producida por la familia.</summary>
public sealed record GuiaEnvio(string Numero, TipoEntrega Familia, string Etiqueta, int PlazoDias, DateTime FechaEstimadaUtc);

/// <summary>Producto abstracto 3: constructor de guía/etiqueta. Solo acepta el plazo de su misma familia.</summary>
public interface IConstructorGuia
{
    TipoEntrega Familia { get; }

    GuiaEnvio Construir(string codigoPedido, int secuencia, DateTime despachoUtc, IPlazoEntrega plazo);
}

/// <summary>Abstract Factory: produce objetos compatibles de una misma familia de entrega.</summary>
public interface IFabricaEntrega
{
    TipoEntrega Tipo { get; }

    string Nombre { get; }

    ITarifaEntrega CrearTarifa();

    IPlazoEntrega CrearPlazo();

    IConstructorGuia CrearConstructorGuia();
}

/// <summary>Base común de los productos de cada familia para evitar duplicación (DRY).</summary>
internal sealed class TarifaFija(TipoEntrega familia, decimal monto) : ITarifaEntrega
{
    public TipoEntrega Familia => familia;

    public Dinero Calcular() => Dinero.Usd(monto);
}

internal sealed class PlazoEnDias(TipoEntrega familia, int dias) : IPlazoEntrega
{
    public TipoEntrega Familia => familia;

    public int Dias => dias;

    public DateTime FechaEstimada(DateTime despachoUtc) => despachoUtc.AddDays(dias);
}

internal sealed class ConstructorGuiaFamilia(TipoEntrega familia, string prefijoEtiqueta) : IConstructorGuia
{
    public TipoEntrega Familia => familia;

    public GuiaEnvio Construir(string codigoPedido, int secuencia, DateTime despachoUtc, IPlazoEntrega plazo)
    {
        if (plazo.Familia != familia)
        {
            throw new ReglaNegocioException(
                "FAMILIA_INCOMPATIBLE",
                $"No se puede construir una guía {familia} con un plazo de la familia {plazo.Familia}.");
        }

        var numero = $"GUIA-{despachoUtc.Year}-{secuencia:0000}";
        var estimada = plazo.FechaEstimada(despachoUtc);
        var etiqueta = $"{prefijoEtiqueta} | {numero} | Pedido {codigoPedido} | Entrega en {plazo.Dias} día(s) | Estimada {estimada:dd/MM/yyyy}";
        return new GuiaEnvio(numero, familia, etiqueta, plazo.Dias, estimada);
    }
}

/// <summary>Familia concreta ESTÁNDAR: USD 5.00, 3 días.</summary>
public sealed class FabricaEntregaEstandar : IFabricaEntrega
{
    public TipoEntrega Tipo => TipoEntrega.Estandar;

    public string Nombre => "ESTÁNDAR";

    public ITarifaEntrega CrearTarifa() => new TarifaFija(Tipo, 5.00m);

    public IPlazoEntrega CrearPlazo() => new PlazoEnDias(Tipo, 3);

    public IConstructorGuia CrearConstructorGuia() => new ConstructorGuiaFamilia(Tipo, "ENVÍO ESTÁNDAR");
}

/// <summary>Familia concreta EXPRÉS: USD 12.00, 1 día.</summary>
public sealed class FabricaEntregaExpres : IFabricaEntrega
{
    public TipoEntrega Tipo => TipoEntrega.Expres;

    public string Nombre => "EXPRÉS";

    public ITarifaEntrega CrearTarifa() => new TarifaFija(Tipo, 12.00m);

    public IPlazoEntrega CrearPlazo() => new PlazoEnDias(Tipo, 1);

    public IConstructorGuia CrearConstructorGuia() => new ConstructorGuiaFamilia(Tipo, "ENVÍO EXPRÉS PRIORITARIO");
}

/// <summary>Registro de familias disponibles: selecciona la fábrica sin condicionales en el caso de uso.</summary>
public sealed class CatalogoFamiliasEntrega
{
    private readonly IReadOnlyDictionary<TipoEntrega, IFabricaEntrega> _fabricas;

    public CatalogoFamiliasEntrega(IEnumerable<IFabricaEntrega> fabricas)
    {
        _fabricas = fabricas.ToDictionary(f => f.Tipo);
    }

    public static CatalogoFamiliasEntrega Oficial() =>
        new(new IFabricaEntrega[] { new FabricaEntregaEstandar(), new FabricaEntregaExpres() });

    public IFabricaEntrega Obtener(TipoEntrega tipo) =>
        _fabricas.TryGetValue(tipo, out var fabrica)
            ? fabrica
            : throw new ReglaNegocioException("ENTREGA_NO_DISPONIBLE", $"No hay familia de entrega {tipo}.");

    public IReadOnlyCollection<IFabricaEntrega> Todas => _fabricas.Values.ToList();
}
