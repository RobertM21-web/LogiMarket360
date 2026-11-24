using FluentAssertions;
using LogiMarket.Application.Comandos;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Demostracion;
using LogiMarket.Application.Devoluciones;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Seguridad;
using LogiMarket.Tests.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Tests.Calidad;

/// <summary>Guardas de R07 (reloj en consultas), R08 (hook de denegación) y R10 (reloj en simuladores).</summary>
public class RefactorizacionIntegracionTests
{
    // Fecha deliberadamente lejana a la del sistema: si algún componente usa DateTime.UtcNow, la prueba lo detecta.
    private readonly RelojFijo _reloj = new(new DateTime(2026, 1, 10, 15, 0, 0, DateTimeKind.Utc));

    private Task<EntornoIntegracion> EntornoAsync() => EntornoIntegracion.CrearAsync(s => s.AddSingleton<IReloj>(_reloj));

    private static async Task<int> EntregadoAsync(EntornoIntegracion e)
    {
        var id = await e.PedidoPagadoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioLogistica>().DespacharAsync(id));
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioLogistica>().EntregarAsync(id));
        return id;
    }

    [Fact]
    public async Task R07_la_alerta_de_devoluciones_por_revisar_usa_el_reloj_de_la_aplicacion()
    {
        await using var e = await EntornoAsync();
        e.IniciarSesion(Rol.Admin);
        await EntregadoAsync(e);

        var dentroDelPlazo = await e.EnAmbitoAsync(sp => sp.GetRequiredService<ConsultasOperativas>().PanelAsync());
        _reloj.Avanzar(TimeSpan.FromDays(31));
        var fueraDelPlazo = await e.EnAmbitoAsync(sp => sp.GetRequiredService<ConsultasOperativas>().PanelAsync());

        dentroDelPlazo.Alertas.Should().Contain(a => a.StartsWith("1 pedido(s) entregado(s) dentro del plazo"));
        fueraDelPlazo.Alertas.Should().NotContain(a => a.Contains("dentro del plazo"));
    }

    [Fact]
    public async Task R10_las_referencias_simuladas_de_cobro_V2_y_reembolso_usan_el_reloj_de_la_aplicacion()
    {
        await using var e = await EntornoAsync();
        e.IniciarSesion(Rol.Admin);
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioDemostracion>().SeleccionarProveedorAsync("V2"));
        var id = await EntregadoAsync(e);
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioDevoluciones>().ProcesarAsync(new SolicitudDevolucion(id, MotivoDevolucion.Defecto, true, ReintegrarStock: true)));

        (await e.ConsultarBdAsync(db => db.Pagos.Select(p => p.Referencia).SingleAsync())).Should().StartWith("V2-20260110-");
        (await e.ConsultarBdAsync(db => db.Devoluciones.Select(d => d.ReferenciaReembolso).SingleAsync())).Should().StartWith("RMB-20260110150000-");
    }

    private sealed class ComandoDenegado(bool esCobro) : IComandoNegocio
    {
        public string Nombre => esCobro ? "CobroDePrueba" : "ConsultaDePrueba";

        public string Entrada => "-";

        public Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct) =>
            throw new AccesoDenegadoException(esCobro ? Operacion.CobrarPedido : Operacion.AjustarInventario, "consulta.demo", Rol.Consulta);

        public void AlDenegarseAcceso(MetricasSesion metricas)
        {
            if (esCobro)
            {
                metricas.RegistrarCobroDenegado();
            }
        }
    }

    [Fact]
    public async Task R08_cada_comando_decide_que_metrica_afecta_su_denegacion_sin_que_el_invocador_conozca_tipos_concretos()
    {
        var metricas = new MetricasSesion();
        var invocador = new InvocadorComandos(Sesiones.Con(Rol.Consulta, "consulta.demo"), new AuditoriaEnMemoria(), metricas, _reloj);

        var cobro = await invocador.EjecutarAsync(new ComandoDenegado(esCobro: true));
        var otro = await invocador.EjecutarAsync(new ComandoDenegado(esCobro: false));

        cobro.Codigo.Should().Be(AccesoDenegadoException.Codigo);
        otro.Codigo.Should().Be(AccesoDenegadoException.Codigo);
        metricas.Instantanea.CobrosDenegados.Should().Be(1, "solo el comando de cobro cuenta como cobro denegado");
    }

    private sealed class ComandoConResultado(ProcesadorOutbox outbox, bool exito) : ComandoConEventos(outbox)
    {
        public override string Nombre => "Prueba";

        public override string Entrada => $"exito={exito}";

        protected override Task<(ResultadoOperacion Resultado, bool GeneroEventos)> EjecutarReceptorAsync(CancellationToken ct) =>
            Task.FromResult((new ResultadoOperacion(exito, exito ? "OK" : "RECHAZO", "-", null), true));
    }

    [Fact]
    public async Task R09_un_comando_con_eventos_solo_despacha_el_outbox_cuando_el_receptor_tuvo_exito()
    {
        await using var e = await EntornoAsync();
        e.IniciarSesion(Rol.Ventas);
        var demo = e.Servicios.GetRequiredService<IModoDemostracion>();
        demo.FallarNotificaciones = true;
        await e.PedidoPagadoAsync("C001", TipoEntrega.Estandar, ("P05", 1));
        demo.FallarNotificaciones = false;

        await e.EnAmbitoAsync(sp => sp.GetRequiredService<InvocadorComandos>().EjecutarAsync(new ComandoConResultado(sp.GetRequiredService<ProcesadorOutbox>(), exito: false)));
        var trasRechazo = await e.ConsultarBdAsync(db => db.Outbox.Select(o => o.Estado).SingleAsync());
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<InvocadorComandos>().EjecutarAsync(new ComandoConResultado(sp.GetRequiredService<ProcesadorOutbox>(), exito: true)));
        var trasExito = await e.ConsultarBdAsync(db => db.Outbox.Select(o => o.Estado).SingleAsync());

        trasRechazo.Should().Be(EstadoOutbox.Pendiente, "un comando rechazado no debe tener efectos colaterales");
        trasExito.Should().Be(EstadoOutbox.Procesado);
    }
}
