using System.Reflection;
using FluentAssertions;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Seguridad;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.WinForms;
using LogiMarket.WinForms.Formularios;
using LogiMarket.WinForms.Mediador;
using LogiMarket.WinForms.Pantallas;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Tests.Presentacion;

/// <summary>P28 Mediator, P15 Singleton en la capa de presentación y composición real de la ventana principal.</summary>
public class PresentacionTests
{
    private sealed class ColegaFalso(string clave) : IColegaPantalla
    {
        public string Clave { get; } = clave;

        public int Invalidaciones { get; private set; }

        public List<(EventoPantalla Evento, object? Dato)> Recibidos { get; } = new();

        public Task InvalidarAsync()
        {
            Invalidaciones++;
            return Task.CompletedTask;
        }

        public void Recibir(EventoPantalla evento, object? dato) => Recibidos.Add((evento, dato));
    }

    private sealed class NavegadorFalso : INavegador
    {
        public List<string> Mostradas { get; } = new();

        public void Mostrar(string clave) => Mostradas.Add(clave);
    }

    private static (FormularioPrincipalMediator Mediador, Dictionary<string, ColegaFalso> Colegas, NavegadorFalso Navegador) Montar()
    {
        var mediador = new FormularioPrincipalMediator();
        var navegador = new NavegadorFalso();
        mediador.AsignarNavegador(navegador);
        var claves = typeof(ClavesPantalla).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetValue(null)!);
        var colegas = claves.ToDictionary(c => c, c => new ColegaFalso(c));
        foreach (var c in colegas.Values)
        {
            mediador.Registrar(c);
        }

        return (mediador, colegas, navegador);
    }

    [Fact]
    public async Task P28_al_confirmar_el_mediador_refresca_catalogo_pedidos_inventario_y_panel_sin_tocar_clientes()
    {
        var (mediador, colegas, _) = Montar();

        await mediador.NotificarAsync(colegas[ClavesPantalla.Cobro], EventoPantalla.PedidoConfirmado, 7);

        foreach (var clave in new[] { ClavesPantalla.Catalogo, ClavesPantalla.Pedidos, ClavesPantalla.Inventario, ClavesPantalla.Panel, ClavesPantalla.Despacho })
        {
            colegas[clave].Invalidaciones.Should().Be(1, $"{clave} debe refrescarse tras confirmar");
        }

        colegas[ClavesPantalla.Clientes].Invalidaciones.Should().Be(0);
        mediador.Bitacora.Should().StartWith("COBRO → PedidoConfirmado");
    }

    [Fact]
    public async Task P28_al_validar_el_mediador_entrega_el_pedido_a_cobro_y_navega_sin_que_el_carrito_conozca_al_cobro()
    {
        var (mediador, colegas, navegador) = Montar();

        await mediador.NotificarAsync(colegas[ClavesPantalla.Carrito], EventoPantalla.PedidoValidado, 42);

        colegas[ClavesPantalla.Cobro].Recibidos.Should().ContainSingle().Which.Should().Be((EventoPantalla.PedidoValidado, (object?)42));
        navegador.Mostradas.Should().Equal(ClavesPantalla.Cobro);
        colegas[ClavesPantalla.Pedidos].Invalidaciones.Should().Be(1);
    }

    [Fact]
    public void P28_ninguna_pantalla_referencia_a_otra_pantalla()
    {
        var pantallas = typeof(PantallaBase).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(PantallaBase))).ToList();

        pantallas.Should().HaveCount(11);
        foreach (var p in pantallas)
        {
            var dependencias = p.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Select(f => f.FieldType)
                .Concat(p.GetConstructors().SelectMany(c => c.GetParameters()).Select(x => x.ParameterType));
            dependencias.Should().NotContain(t => t.IsSubclassOf(typeof(PantallaBase)) || t == typeof(FormularioPrincipal), $"{p.Name} solo debe conocer al mediador");
        }
    }

    [Fact]
    public async Task Composition_root_valida_el_grafo_y_construye_la_ventana_con_sus_11_pantallas()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "LogiMarketPruebas", Guid.NewGuid().ToString("N"));
        var opciones = new OpcionesLogiMarket { RutaBaseDatos = Path.Combine(carpeta, "ui.db"), CarpetaComprobantes = Path.Combine(carpeta, "comprobantes") };
        try
        {
            await using var servicios = Program.ConfigurarServicios(opciones); // ValidateOnBuild = true
            await using (var ambito = servicios.CreateAsyncScope())
            {
                await ambito.ServiceProvider.GetRequiredService<InicializadorBaseDatos>().InicializarAsync();
            }

            servicios.GetRequiredService<ISesionActual>().Iniciar(new UsuarioSesion(1, "admin.demo", "Administrador", Rol.Admin));
            MetricasSesion? deVentana = null;
            Exception? error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    using var ambitoUi = servicios.CreateScope();
                    using var ventana = ambitoUi.ServiceProvider.GetRequiredService<FormularioPrincipal>();
                    deVentana = ambitoUi.ServiceProvider.GetRequiredService<MetricasSesion>();
                    var mediador = ambitoUi.ServiceProvider.GetRequiredService<FormularioPrincipalMediator>();
                    mediador.NotificarAsync(new ColegaFalso("PRUEBA"), EventoPantalla.CatalogoActualizado).GetAwaiter().GetResult();
                    mediador.Bitacora.Count(l => l.StartsWith("  invalidar", StringComparison.Ordinal)).Should().Be(4, "las pantallas reales están registradas en el mediador");
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();

            error.Should().BeNull();
            deVentana.Should().BeSameAs(servicios.GetRequiredService<MetricasSesion>(), "P15: la ventana y el resto de la app comparten el Singleton");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(carpeta, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
