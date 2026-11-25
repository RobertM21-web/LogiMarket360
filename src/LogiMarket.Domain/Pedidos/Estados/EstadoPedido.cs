using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Pedidos.Estados;

public enum CodigoEstadoPedido
{
    Borrador = 0,
    Validado = 1,
    Pagado = 2,
    Despachado = 3,
    Entregado = 4,
    Cancelado = 5,
}

/// <summary>
/// State: cada estado decide qué acciones admite. Por defecto toda acción se rechaza; cada subclase
/// sobrescribe solo las transiciones válidas. No existe un <c>switch</c> central de transiciones.
/// </summary>
public abstract class EstadoPedido
{
    private static readonly IReadOnlyDictionary<CodigoEstadoPedido, EstadoPedido> Instancias =
        new EstadoPedido[]
        {
            new EstadoBorrador(), new EstadoValidado(), new EstadoPagado(),
            new EstadoDespachado(), new EstadoEntregado(), new EstadoCancelado(),
        }.ToDictionary(e => e.Codigo);

    public abstract CodigoEstadoPedido Codigo { get; }

    public string Nombre => Codigo.ToString().ToUpperInvariant();

    public virtual bool PermiteEditar => false;

    public virtual bool PermiteValidar => false;

    public virtual bool PermiteCobrar => false;

    public virtual bool PermiteDespachar => false;

    public virtual bool PermiteEntregar => false;

    public virtual bool PermiteCancelar => false;

    public virtual bool PermiteDevolver => false;

    public static EstadoPedido De(CodigoEstadoPedido codigo) => Instancias[codigo];

    /// <summary>Registra el hito temporal al entrar en el estado.</summary>
    internal virtual void AlEntrar(Pedido pedido, DateTime ahoraUtc)
    {
    }

    internal virtual void EditarContenido(Pedido pedido) => throw Invalida("editar");

    internal virtual void Validar(Pedido pedido, string usuario, DateTime ahoraUtc) => throw Invalida("validar");

    internal virtual void RegistrarPago(Pedido pedido, string usuario, DateTime ahoraUtc) => throw Invalida("cobrar");

    internal virtual void Despachar(Pedido pedido, string usuario, DateTime ahoraUtc) => throw Invalida("despachar");

    internal virtual void Entregar(Pedido pedido, string usuario, DateTime ahoraUtc) => throw Invalida("entregar");

    internal virtual void Cancelar(Pedido pedido, string usuario, DateTime ahoraUtc) => throw Invalida("cancelar");

    internal virtual void Devolver(Pedido pedido) => throw Invalida("devolver");

    protected ReglaNegocioException Invalida(string accion) =>
        new("TRANSICION_INVALIDA", $"No se puede {accion} un pedido en estado {Nombre}.");
}

internal sealed class EstadoBorrador : EstadoPedido
{
    public override CodigoEstadoPedido Codigo => CodigoEstadoPedido.Borrador;

    public override bool PermiteEditar => true;

    public override bool PermiteValidar => true;

    public override bool PermiteCancelar => true;

    internal override void EditarContenido(Pedido pedido)
    {
        // Edición permitida: el borrador aún no compromete cobro ni stock.
    }

    internal override void Validar(Pedido pedido, string usuario, DateTime ahoraUtc) =>
        pedido.TransicionarA(CodigoEstadoPedido.Validado, usuario, ahoraUtc);

    internal override void Cancelar(Pedido pedido, string usuario, DateTime ahoraUtc) =>
        pedido.TransicionarA(CodigoEstadoPedido.Cancelado, usuario, ahoraUtc);
}

internal sealed class EstadoValidado : EstadoPedido
{
    public override CodigoEstadoPedido Codigo => CodigoEstadoPedido.Validado;

    internal override void AlEntrar(Pedido pedido, DateTime ahoraUtc) => pedido.FechaValidacionUtc = ahoraUtc;

    public override bool PermiteCobrar => true;

    public override bool PermiteCancelar => true;

    internal override void RegistrarPago(Pedido pedido, string usuario, DateTime ahoraUtc) =>
        pedido.TransicionarA(CodigoEstadoPedido.Pagado, usuario, ahoraUtc);

    internal override void Cancelar(Pedido pedido, string usuario, DateTime ahoraUtc) =>
        pedido.TransicionarA(CodigoEstadoPedido.Cancelado, usuario, ahoraUtc);
}

internal sealed class EstadoPagado : EstadoPedido
{
    public override CodigoEstadoPedido Codigo => CodigoEstadoPedido.Pagado;

    internal override void AlEntrar(Pedido pedido, DateTime ahoraUtc) => pedido.FechaPagoUtc = ahoraUtc;

    public override bool PermiteDespachar => true;

    internal override void Despachar(Pedido pedido, string usuario, DateTime ahoraUtc) =>
        pedido.TransicionarA(CodigoEstadoPedido.Despachado, usuario, ahoraUtc);
}

internal sealed class EstadoDespachado : EstadoPedido
{
    public override CodigoEstadoPedido Codigo => CodigoEstadoPedido.Despachado;

    internal override void AlEntrar(Pedido pedido, DateTime ahoraUtc) => pedido.FechaDespachoUtc = ahoraUtc;

    public override bool PermiteEntregar => true;

    internal override void Entregar(Pedido pedido, string usuario, DateTime ahoraUtc) =>
        pedido.TransicionarA(CodigoEstadoPedido.Entregado, usuario, ahoraUtc);
}

internal sealed class EstadoEntregado : EstadoPedido
{
    public override CodigoEstadoPedido Codigo => CodigoEstadoPedido.Entregado;

    internal override void AlEntrar(Pedido pedido, DateTime ahoraUtc) => pedido.FechaEntregaUtc = ahoraUtc;

    public override bool PermiteDevolver => true;

    internal override void Devolver(Pedido pedido)
    {
        // La devolución no cambia el estado del ciclo; el agregado marca la devolución única.
    }
}

internal sealed class EstadoCancelado : EstadoPedido
{
    public override CodigoEstadoPedido Codigo => CodigoEstadoPedido.Cancelado;

    internal override void AlEntrar(Pedido pedido, DateTime ahoraUtc) => pedido.FechaCancelacionUtc = ahoraUtc;
}
