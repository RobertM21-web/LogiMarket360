using LogiMarket.Application.Comandos;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Pedidos.Borrador;
using LogiMarket.Domain.Seguridad;
using LogiMarket.Infrastructure;
using LogiMarket.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Tests.Soporte;

/// <summary>
/// Entorno de integración aislado: base SQLite en un archivo temporal nuevo, grafo DI real y semilla.
/// Cada prueba crea el suyo (base de pruebas controlada y restablecida).
/// </summary>
public sealed class EntornoIntegracion : IAsyncDisposable
{
    private readonly Action<IServiceCollection>? _personalizar;
    private ServiceProvider _proveedor;

    private EntornoIntegracion(OpcionesLogiMarket opciones, Action<IServiceCollection>? personalizar)
    {
        Opciones = opciones;
        _personalizar = personalizar;
        _proveedor = Construir();
    }

    public OpcionesLogiMarket Opciones { get; }

    public IServiceProvider Servicios => _proveedor;

    public static async Task<EntornoIntegracion> CrearAsync(Action<IServiceCollection>? personalizar = null, bool inicializar = true)
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "LogiMarketPruebas", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        var entorno = new EntornoIntegracion(
            new OpcionesLogiMarket { RutaBaseDatos = Path.Combine(carpeta, "prueba.db"), CarpetaComprobantes = Path.Combine(carpeta, "comprobantes") },
            personalizar);
        if (inicializar)
        {
            await entorno.EnAmbitoAsync(sp => sp.GetRequiredService<InicializadorBaseDatos>().InicializarAsync());
        }

        return entorno;
    }

    /// <summary>Simula cerrar y reabrir la aplicación: descarta el contenedor y crea uno nuevo sobre el mismo archivo.</summary>
    public async Task ReiniciarAsync()
    {
        await _proveedor.DisposeAsync();
        SqliteConnection.ClearAllPools();
        _proveedor = Construir();
        await EnAmbitoAsync(sp => sp.GetRequiredService<InicializadorBaseDatos>().InicializarAsync());
    }

    public void IniciarSesion(Rol rol)
    {
        var usuario = SemillaDatos.CuentasDemo.First(c => c.Rol == rol);
        _proveedor.GetRequiredService<ISesionActual>().Iniciar(new UsuarioSesion((int)rol + 1, usuario.Usuario, usuario.Nombre, rol));
    }

    public async Task<T> EnAmbitoAsync<T>(Func<IServiceProvider, Task<T>> accion)
    {
        await using var ambito = _proveedor.CreateAsyncScope();
        return await accion(ambito.ServiceProvider);
    }

    public Task EnAmbitoAsync(Func<IServiceProvider, Task> accion) =>
        EnAmbitoAsync(async sp =>
        {
            await accion(sp);
            return true;
        });

    public Task<T> ConsultarBdAsync<T>(Func<LogiMarketDbContext, Task<T>> consulta) =>
        EnAmbitoAsync(sp => consulta(sp.GetRequiredService<LogiMarketDbContext>()));

    public Task<int> StockAsync(string sku) =>
        ConsultarBdAsync(db => db.Productos.Where(p => p.Sku == sku).Select(p => p.Stock).SingleAsync());

    /// <summary>Arma un borrador con el cliente y las líneas dadas y lo valida (crea el pedido VALIDADO).</summary>
    public Task<ResultadoValidacionPedido> ValidarPedidoAsync(string codigoCliente, TipoEntrega entrega, params (string Codigo, int Cantidad)[] lineas) =>
        EnAmbitoAsync(async sp =>
        {
            var servicio = sp.GetRequiredService<ServicioPedidos>();
            var arbol = await servicio.CargarCatalogoAsync();
            var cliente = await sp.GetRequiredService<LogiMarketDbContext>().Clientes.AsNoTracking().SingleAsync(c => c.Codigo == codigoCliente);
            var borrador = new PedidoBorrador();
            borrador.SeleccionarCliente(cliente);
            borrador.CambiarEntrega(entrega);
            foreach (var (codigo, cantidad) in lineas)
            {
                borrador.AgregarLinea(arbol.Obtener(codigo), cantidad);
            }

            return await servicio.GuardarYValidarAsync(borrador);
        });

    /// <summary>Confirma con cobro mediante el Command y el invocador (mismo camino que la UI).</summary>
    public Task<ResultadoOperacion> ConfirmarAsync(int pedidoId) =>
        EnAmbitoAsync(sp => sp.GetRequiredService<InvocadorComandos>().EjecutarAsync(sp.GetRequiredService<FabricaComandos>().Confirmar(pedidoId)));

    public async Task<int> PedidoPagadoAsync(string cliente, TipoEntrega entrega, params (string, int)[] lineas)
    {
        var validado = await ValidarPedidoAsync(cliente, entrega, lineas);
        if (!validado.Exito)
        {
            throw new InvalidOperationException(validado.Mensaje);
        }

        var confirmado = await ConfirmarAsync(validado.PedidoId!.Value);
        return confirmado.Exito ? validado.PedidoId.Value : throw new InvalidOperationException(confirmado.Mensaje);
    }

    public async ValueTask DisposeAsync()
    {
        await _proveedor.DisposeAsync();
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Path.GetDirectoryName(Opciones.RutaBaseDatos)!, recursive: true);
        }
        catch (IOException)
        {
            // Archivo retenido brevemente por el sistema: la carpeta temporal se limpia en la siguiente ejecución.
        }
    }

    private ServiceProvider Construir()
    {
        var servicios = new ServiceCollection().AgregarLogiMarket(Opciones);
        _personalizar?.Invoke(servicios);
        return servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
