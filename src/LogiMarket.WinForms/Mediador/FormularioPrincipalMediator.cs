namespace LogiMarket.WinForms.Mediador;

/// <summary>Hechos que una pantalla comunica al mediador tras una operación.</summary>
public enum EventoPantalla
{
    PedidoValidado,
    PedidoConfirmado,
    PedidoActualizado,
    CatalogoActualizado,
    ClientesActualizados,
    InventarioActualizado,
    DevolucionRegistrada,
    EscenarioAplicado,
    AbrirPedido,
}

/// <summary>Claves de las pantallas registradas en el mediador.</summary>
public static class ClavesPantalla
{
    public const string Panel = "PANEL";
    public const string Catalogo = "CATALOGO";
    public const string Clientes = "CLIENTES";
    public const string Carrito = "CARRITO";
    public const string Pedidos = "PEDIDOS";
    public const string Cobro = "COBRO";
    public const string Despacho = "DESPACHO";
    public const string Devoluciones = "DEVOLUCIONES";
    public const string Inventario = "INVENTARIO";
    public const string Reportes = "REPORTES";
    public const string Auditoria = "AUDITORIA";
}

/// <summary>Colega del Mediator: una pantalla que solo conoce al mediador, nunca a otras pantallas.</summary>
public interface IColegaPantalla
{
    string Clave { get; }

    /// <summary>Marca la vista como desactualizada (se recarga ya si está visible o al mostrarse).</summary>
    Task InvalidarAsync();

    /// <summary>Recibe un dato contextual (p. ej. el pedido a cobrar) enviado por el mediador.</summary>
    void Recibir(EventoPantalla evento, object? dato);
}

/// <summary>Muestra una pantalla en el contenedor principal (lo implementa el formulario principal).</summary>
public interface INavegador
{
    void Mostrar(string clave);
}

public interface IMediadorPantallas
{
    void Registrar(IColegaPantalla colega);

    Task NotificarAsync(IColegaPantalla emisor, EventoPantalla evento, object? dato = null);
}

/// <summary>
/// Mediator: decide qué vistas deben refrescarse o recibir contexto tras cada operación. Las pantallas
/// solo notifican hechos; ninguna referencia ni invoca a otra.
/// </summary>
public sealed class FormularioPrincipalMediator : IMediadorPantallas
{
    private static readonly IReadOnlyDictionary<EventoPantalla, string[]> Reglas = new Dictionary<EventoPantalla, string[]>
    {
        [EventoPantalla.PedidoValidado] = [ClavesPantalla.Pedidos, ClavesPantalla.Panel, ClavesPantalla.Cobro],
        [EventoPantalla.PedidoConfirmado] = [ClavesPantalla.Catalogo, ClavesPantalla.Carrito, ClavesPantalla.Pedidos, ClavesPantalla.Inventario, ClavesPantalla.Despacho, ClavesPantalla.Panel, ClavesPantalla.Reportes, ClavesPantalla.Auditoria],
        [EventoPantalla.PedidoActualizado] = [ClavesPantalla.Pedidos, ClavesPantalla.Cobro, ClavesPantalla.Despacho, ClavesPantalla.Devoluciones, ClavesPantalla.Panel, ClavesPantalla.Auditoria],
        [EventoPantalla.CatalogoActualizado] = [ClavesPantalla.Catalogo, ClavesPantalla.Carrito, ClavesPantalla.Inventario, ClavesPantalla.Panel],
        [EventoPantalla.ClientesActualizados] = [ClavesPantalla.Clientes, ClavesPantalla.Carrito],
        [EventoPantalla.InventarioActualizado] = [ClavesPantalla.Inventario, ClavesPantalla.Catalogo, ClavesPantalla.Carrito, ClavesPantalla.Panel, ClavesPantalla.Auditoria],
        [EventoPantalla.DevolucionRegistrada] = [ClavesPantalla.Devoluciones, ClavesPantalla.Pedidos, ClavesPantalla.Inventario, ClavesPantalla.Catalogo, ClavesPantalla.Panel, ClavesPantalla.Reportes, ClavesPantalla.Auditoria],
        [EventoPantalla.EscenarioAplicado] = [ClavesPantalla.Auditoria, ClavesPantalla.Cobro, ClavesPantalla.Panel],
        [EventoPantalla.AbrirPedido] = [],
    };

    private readonly Dictionary<string, IColegaPantalla> _colegas = new();
    private readonly List<string> _bitacora = new();
    private INavegador? _navegador;

    /// <summary>Acciones de coordinación realizadas (visible en pruebas y diagnóstico).</summary>
    public IReadOnlyList<string> Bitacora => _bitacora;

    public void AsignarNavegador(INavegador navegador) => _navegador = navegador;

    public void Registrar(IColegaPantalla colega) => _colegas[colega.Clave] = colega;

    public async Task NotificarAsync(IColegaPantalla emisor, EventoPantalla evento, object? dato = null)
    {
        _bitacora.Add($"{emisor.Clave} → {evento}");
        foreach (var clave in Reglas[evento])
        {
            if (_colegas.TryGetValue(clave, out var colega))
            {
                _bitacora.Add($"  invalidar {clave}");
                await colega.InvalidarAsync();
            }
        }

        // Contexto y navegación derivados del hecho (el emisor no sabe quién lo atiende).
        switch (evento)
        {
            case EventoPantalla.PedidoValidado:
                Enviar(ClavesPantalla.Cobro, evento, dato);
                Navegar(ClavesPantalla.Cobro);
                break;
            case EventoPantalla.AbrirPedido when dato is (string destino, int):
                Enviar(destino, evento, dato);
                Navegar(destino);
                break;
        }
    }

    private void Enviar(string clave, EventoPantalla evento, object? dato)
    {
        if (_colegas.TryGetValue(clave, out var colega))
        {
            _bitacora.Add($"  enviar {evento} a {clave}");
            colega.Recibir(evento, dato);
        }
    }

    private void Navegar(string clave)
    {
        _bitacora.Add($"  mostrar {clave}");
        _navegador?.Mostrar(clave);
    }
}
