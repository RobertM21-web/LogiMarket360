namespace LogiMarket.Domain.Pedidos.Estados;

/// <summary>
/// Nombres canónicos de los estados del pedido tal como se exponen en lecturas, filtros y la interfaz.
/// Fuente única: coinciden con <see cref="EstadoPedido.Nombre"/> (verificado por prueba).
/// </summary>
public static class NombresEstado
{
    public const string Borrador = "BORRADOR";
    public const string Validado = "VALIDADO";
    public const string Pagado = "PAGADO";
    public const string Despachado = "DESPACHADO";
    public const string Entregado = "ENTREGADO";
    public const string Cancelado = "CANCELADO";

    /// <summary>Todos los estados, en el orden de <see cref="CodigoEstadoPedido"/>.</summary>
    public static IReadOnlyList<string> Todos { get; } = [Borrador, Validado, Pagado, Despachado, Entregado, Cancelado];

    /// <summary>Recorrido normal de un pedido sin cancelación.</summary>
    public static IReadOnlyList<string> Flujo { get; } = [Borrador, Validado, Pagado, Despachado, Entregado];

    /// <summary>Estados que aún requieren gestión operativa.</summary>
    public static IReadOnlyList<string> Activos { get; } = [Borrador, Validado, Pagado, Despachado];
}
