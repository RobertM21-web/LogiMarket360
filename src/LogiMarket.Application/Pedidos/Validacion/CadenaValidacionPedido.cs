using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo.Recorrido;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Pedidos.Borrador;
using LogiMarket.Domain.Pedidos.Estados;

namespace LogiMarket.Application.Pedidos.Validacion;

/// <summary>Datos a validar: provienen de un borrador (antes de persistir) o de un pedido existente.</summary>
public sealed record SolicitudValidacion(
    Cliente? Cliente,
    IReadOnlyList<(string Codigo, int Cantidad)> Lineas,
    IReadOnlyList<RequerimientoStock> Requerimientos,
    EstadoPedido? Estado,
    bool ParaConfirmar)
{
    public static SolicitudValidacion DesdeBorrador(PedidoBorrador borrador) => new(
        borrador.Cliente,
        borrador.Lineas.Select(l => (l.Elemento.Codigo, l.Cantidad)).ToList(),
        borrador.Lineas
            .SelectMany(l => ExpansionSku.Expandir(l.Elemento, l.Cantidad))
            .GroupBy(r => (r.Producto.ProductoId, r.Producto.Sku))
            .Select(g => new RequerimientoStock(g.Key.ProductoId, g.Key.Sku, g.Sum(r => r.Cantidad)))
            .ToList(),
        null,
        ParaConfirmar: false);

    public static SolicitudValidacion DesdePedido(Pedido pedido, bool paraConfirmar) => new(
        pedido.Cliente,
        pedido.Detalles.Select(d => (d.Codigo, d.Cantidad)).ToList(),
        pedido.RequerimientosStock(),
        pedido.Estado,
        paraConfirmar);
}

public sealed record RechazoValidacion(string Eslabon, string Codigo, string Mensaje);

/// <summary>Resultado estructurado de la cadena con el recorrido de eslabones evaluados.</summary>
public sealed record ResultadoValidacion(RechazoValidacion? Rechazo, IReadOnlyList<string> Recorrido)
{
    public bool EsValido => Rechazo is null;
}

/// <summary>Chain of Responsibility: cada eslabón valida un aspecto y delega o corta con un rechazo.</summary>
public abstract class EslabonValidacion
{
    private EslabonValidacion? _siguiente;

    public abstract string Nombre { get; }

    public EslabonValidacion Enlazar(EslabonValidacion siguiente)
    {
        _siguiente = siguiente;
        return siguiente;
    }

    public async Task<ResultadoValidacion> ValidarAsync(SolicitudValidacion solicitud, List<string> recorrido, CancellationToken ct)
    {
        recorrido.Add(Nombre);
        var rechazo = await EvaluarAsync(solicitud, ct);
        if (rechazo is not null || _siguiente is null)
        {
            return new ResultadoValidacion(rechazo, recorrido);
        }

        return await _siguiente.ValidarAsync(solicitud, recorrido, ct);
    }

    protected abstract Task<RechazoValidacion?> EvaluarAsync(SolicitudValidacion solicitud, CancellationToken ct);

    protected RechazoValidacion Rechazar(string codigo, string mensaje) => new(Nombre, codigo, mensaje);
}

/// <summary>Eslabón 1: el pedido tiene un cliente activo.</summary>
public sealed class ClienteActivoValidador : EslabonValidacion
{
    public override string Nombre => "Cliente activo";

    protected override Task<RechazoValidacion?> EvaluarAsync(SolicitudValidacion s, CancellationToken ct) =>
        Task.FromResult(s.Cliente switch
        {
            null => Rechazar("PEDIDO_SIN_CLIENTE", "Seleccione un cliente para el pedido."),
            { Activo: false } c => Rechazar("CLIENTE_INACTIVO", $"El cliente {c.Nombre} está inactivo."),
            _ => null,
        });
}

/// <summary>Eslabón 2: hay líneas y todas tienen cantidades positivas.</summary>
public sealed class LineasValidador : EslabonValidacion
{
    public override string Nombre => "Líneas y cantidades";

    protected override Task<RechazoValidacion?> EvaluarAsync(SolicitudValidacion s, CancellationToken ct)
    {
        if (s.Lineas.Count == 0)
        {
            return Task.FromResult<RechazoValidacion?>(Rechazar("PEDIDO_SIN_LINEAS", "Agregue al menos un producto o kit."));
        }

        var invalida = s.Lineas.FirstOrDefault(l => l.Cantidad <= 0);
        return Task.FromResult(invalida.Codigo is null
            ? null
            : Rechazar("CANTIDAD_INVALIDA", $"La cantidad de {invalida.Codigo} debe ser mayor que cero."));
    }
}

/// <summary>Eslabón 3: existencias suficientes de cada SKU final (kits expandidos).</summary>
public sealed class StockValidador : EslabonValidacion
{
    private readonly IRepositorioProductos _productos;

    public StockValidador(IRepositorioProductos productos)
    {
        _productos = productos;
    }

    public override string Nombre => "Reglas de stock";

    protected override async Task<RechazoValidacion?> EvaluarAsync(SolicitudValidacion s, CancellationToken ct)
    {
        var productos = (await _productos.ListarPorIdsAsync(s.Requerimientos.Select(r => r.ProductoId), ct)).ToDictionary(p => p.Id);
        foreach (var req in s.Requerimientos)
        {
            if (!productos.TryGetValue(req.ProductoId, out var producto) || !producto.Activo)
            {
                return Rechazar("PRODUCTO_NO_DISPONIBLE", $"El producto {req.Sku} no está disponible.");
            }

            if (!producto.TieneStock(req.Cantidad))
            {
                return Rechazar("STOCK_INSUFICIENTE", $"Stock insuficiente de {req.Sku}: se requieren {req.Cantidad} y hay {producto.Stock}.");
            }
        }

        return null;
    }
}

/// <summary>Eslabón 4: el usuario puede ejecutar la operación y el estado la admite.</summary>
public sealed class CondicionesConfirmacionValidador : EslabonValidacion
{
    private readonly IAutorizador _autorizador;

    public CondicionesConfirmacionValidador(IAutorizador autorizador)
    {
        _autorizador = autorizador;
    }

    public override string Nombre => "Autorización y condiciones";

    protected override Task<RechazoValidacion?> EvaluarAsync(SolicitudValidacion s, CancellationToken ct)
    {
        var operacion = s.ParaConfirmar ? Operacion.CobrarPedido : Operacion.ValidarPedido;
        if (!_autorizador.Puede(operacion))
        {
            return Task.FromResult<RechazoValidacion?>(Rechazar(AccesoDenegadoException.Codigo, $"Su rol no permite {(s.ParaConfirmar ? "confirmar y cobrar" : "validar")} pedidos."));
        }

        var estadoAdmite = s.Estado is null
            || (s.ParaConfirmar ? s.Estado.PermiteCobrar : s.Estado.PermiteValidar);
        return Task.FromResult(estadoAdmite
            ? null
            : Rechazar("ESTADO_NO_ADMITE", $"Un pedido {s.Estado!.Nombre} no puede {(s.ParaConfirmar ? "confirmarse" : "validarse")}."));
    }
}

/// <summary>Arma la cadena en el orden oficial: cliente → líneas → stock → autorización/condiciones.</summary>
public sealed class CadenaValidacionPedido
{
    private readonly EslabonValidacion _primero;

    public CadenaValidacionPedido(IRepositorioProductos productos, IAutorizador autorizador)
    {
        _primero = new ClienteActivoValidador();
        _primero
            .Enlazar(new LineasValidador())
            .Enlazar(new StockValidador(productos))
            .Enlazar(new CondicionesConfirmacionValidador(autorizador));
    }

    public Task<ResultadoValidacion> ValidarAsync(SolicitudValidacion solicitud, CancellationToken ct = default) =>
        _primero.ValidarAsync(solicitud, new List<string>(), ct);
}
