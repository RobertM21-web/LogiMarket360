using LogiMarket.Domain.Clientes;

namespace LogiMarket.Domain.Promociones;

/// <summary>Datos que las políticas necesitan para decidir si aplican.</summary>
public sealed record ContextoDescuento(CategoriaCliente Categoria, int UnidadesFisicas);

/// <summary>
/// Strategy: política de descuento. Las políticas son excluyentes; el selector elige la aplicable
/// de mayor <see cref="Prioridad"/>.
/// </summary>
public interface IPoliticaDescuento
{
    string Codigo { get; }

    /// <summary>Mayor número = mayor prioridad.</summary>
    int Prioridad { get; }

    decimal Tasa { get; }

    bool Aplica(ContextoDescuento contexto);
}

/// <summary>NORMAL 0 %: siempre aplica, con la menor prioridad.</summary>
public sealed class PoliticaSinDescuento : IPoliticaDescuento
{
    public string Codigo => "NORMAL";

    public int Prioridad => 0;

    public decimal Tasa => 0m;

    public bool Aplica(ContextoDescuento contexto) => true;
}

/// <summary>VIP 10 %: clientes de categoría VIP.</summary>
public sealed class PoliticaVip10 : IPoliticaDescuento
{
    public string Codigo => "VIP";

    public int Prioridad => 10;

    public decimal Tasa => 0.10m;

    public bool Aplica(ContextoDescuento contexto) => contexto.Categoria == CategoriaCliente.Vip;
}

/// <summary>MAYORISTA 15 %: 10 o más unidades físicas finales, sin importar la categoría del cliente.</summary>
public sealed class PoliticaMayorista15 : IPoliticaDescuento
{
    public const int UnidadesMinimas = 10;

    public string Codigo => "MAYORISTA";

    public int Prioridad => 20;

    public decimal Tasa => 0.15m;

    public bool Aplica(ContextoDescuento contexto) => contexto.UnidadesFisicas >= UnidadesMinimas;
}
