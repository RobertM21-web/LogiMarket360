using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Promociones;

/// <summary>
/// Elige una única política aplicable por prioridad explícita (sin acumulación). Añadir una política
/// nueva solo requiere registrarla; no existe una cadena central de <c>if</c>.
/// </summary>
public sealed class SelectorPoliticaDescuento
{
    private readonly IReadOnlyList<IPoliticaDescuento> _politicas;

    public SelectorPoliticaDescuento(IEnumerable<IPoliticaDescuento> politicas)
    {
        _politicas = politicas.OrderByDescending(p => p.Prioridad).ToList();
        if (_politicas.Count == 0)
        {
            throw new ArgumentException("Se requiere al menos una política de descuento.", nameof(politicas));
        }

        var duplicada = _politicas.GroupBy(p => p.Prioridad).FirstOrDefault(g => g.Count() > 1);
        if (duplicada is not null)
        {
            throw new ArgumentException($"Prioridad duplicada {duplicada.Key}: la selección sería ambigua.", nameof(politicas));
        }
    }

    public static SelectorPoliticaDescuento Oficial() =>
        new(new IPoliticaDescuento[] { new PoliticaMayorista15(), new PoliticaVip10(), new PoliticaSinDescuento() });

    public IPoliticaDescuento Seleccionar(ContextoDescuento contexto) =>
        _politicas.FirstOrDefault(p => p.Aplica(contexto))
        ?? throw new ReglaNegocioException("SIN_POLITICA", "Ninguna política de descuento aplica al pedido.");
}
