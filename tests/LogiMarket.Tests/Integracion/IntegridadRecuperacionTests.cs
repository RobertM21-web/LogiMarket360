using FluentAssertions;
using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Devoluciones;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Seguridad;
using LogiMarket.Tests.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace LogiMarket.Tests.Integracion;

/// <summary>P33 Template Method y grupo E: P35 devolución, P36 concurrencia, P37 recuperación del Outbox.</summary>
public class IntegridadRecuperacionTests(ITestOutputHelper salida)
{
    private readonly RelojFijo _reloj = new(new DateTime(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc));

    private Task<EntornoIntegracion> EntornoConRelojAsync() =>
        EntornoIntegracion.CrearAsync(s => s.AddSingleton<IReloj>(_reloj));

    private async Task<int> EntregadoAsync(EntornoIntegracion e, string cliente, params (string, int)[] lineas)
    {
        e.IniciarSesion(Rol.Admin);
        var id = await e.PedidoPagadoAsync(cliente, TipoEntrega.Estandar, lineas);
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioLogistica>().DespacharAsync(id));
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioLogistica>().EntregarAsync(id));
        return id;
    }

    private static Task<ResultadoDevolucion> DevolverAsync(EntornoIntegracion e, int id, MotivoDevolucion motivo, bool sinUso = true) =>
        e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioDevoluciones>().ProcesarAsync(new SolicitudDevolucion(id, motivo, sinUso, ReintegrarStock: true)));

    [Fact]
    public async Task P33_template_method_defecto_dia_20_aceptado_y_arrepentimiento_dia_10_rechazado()
    {
        await using var e = await EntornoConRelojAsync();
        var ana = await EntregadoAsync(e, "C001", ("KIT_TELETRABAJO", 1));
        var luis = await EntregadoAsync(e, "C002", ("P04", 2));
        var stockP04 = await e.StockAsync("P04");

        _reloj.Avanzar(TimeSpan.FromDays(10));
        var arrepentimiento = await DevolverAsync(e, luis, MotivoDevolucion.Arrepentimiento);
        _reloj.Avanzar(TimeSpan.FromDays(10));
        var defecto = await DevolverAsync(e, ana, MotivoDevolucion.Defecto);

        arrepentimiento.Exito.Should().BeFalse();
        arrepentimiento.Codigo.Should().Be("FUERA_DE_PLAZO");
        arrepentimiento.Pasos.Should().Equal("1. Verificar elegibilidad", "2. Inspeccionar condición");
        (await e.StockAsync("P04")).Should().Be(stockP04, "un rechazo no altera stock");
        defecto.Exito.Should().BeTrue();
        defecto.Pasos.Should().HaveCount(5);
        (await e.ConsultarBdAsync(db => db.Devoluciones.CountAsync())).Should().Be(1, "el rechazo no genera reembolso");
    }

    [Fact]
    public async Task P33_arrepentimiento_exige_producto_sin_uso()
    {
        await using var e = await EntornoConRelojAsync();
        var id = await EntregadoAsync(e, "C003", ("P05", 2));
        _reloj.Avanzar(TimeSpan.FromDays(2));

        var usado = await DevolverAsync(e, id, MotivoDevolucion.Arrepentimiento, sinUso: false);
        var sinUso = await DevolverAsync(e, id, MotivoDevolucion.Arrepentimiento, sinUso: true);

        usado.Codigo.Should().Be("PRODUCTO_USADO");
        sinUso.Exito.Should().BeTrue();
        sinUso.Reembolso.Should().Be(10.00m);
    }

    [Fact]
    public async Task P35_devolucion_de_ana_por_defecto_reembolsa_54_sin_flete_una_sola_vez()
    {
        await using var e = await EntornoConRelojAsync();
        var id = await EntregadoAsync(e, "C001", ("KIT_TELETRABAJO", 1));
        _reloj.Avanzar(TimeSpan.FromDays(5));

        var primera = await DevolverAsync(e, id, MotivoDevolucion.Defecto);
        var segunda = await DevolverAsync(e, id, MotivoDevolucion.Defecto);

        primera.Exito.Should().BeTrue();
        primera.Reembolso.Should().Be(54.00m);
        segunda.Exito.Should().BeFalse();
        segunda.Codigo.Should().Be("DEVOLUCION_DUPLICADA");
        (await e.StockAsync("P01"), await e.StockAsync("P02"), await e.StockAsync("P03")).Should().Be((8, 5, 20));
        var reintegros = await e.ConsultarBdAsync(db => db.Movimientos.Where(m => m.Motivo == MotivoMovimiento.Devolucion).Select(m => m.Descripcion).ToListAsync());
        (await e.ConsultarBdAsync(db => db.Movimientos.CountAsync(m => m.Motivo == MotivoMovimiento.Devolucion))).Should().Be(3);
        (await e.ConsultarBdAsync(db => db.Devoluciones.SingleAsync())).MontoReembolso.Valor.Should().Be(54.00m);
        _ = reintegros;
    }

    [Fact]
    public async Task P36_dos_cobros_concurrentes_sobre_stock_1_aprueban_como_maximo_uno()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Admin);
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioInventario>().AjustarAsync("P04", -3, "Preparar prueba de concurrencia"));
        var a = (await e.ValidarPedidoAsync("C002", TipoEntrega.Estandar, ("P04", 1))).PedidoId!.Value;
        var b = (await e.ValidarPedidoAsync("C003", TipoEntrega.Estandar, ("P04", 1))).PedidoId!.Value;

        var resultados = await Task.WhenAll(
            Task.Run(() => e.EnAmbitoAsync(sp => sp.GetRequiredService<ConfirmacionPedidoFacade>().ConfirmarAsync(a))),
            Task.Run(() => e.EnAmbitoAsync(sp => sp.GetRequiredService<ConfirmacionPedidoFacade>().ConfirmarAsync(b))));

        foreach (var r in resultados)
        {
            salida.WriteLine($"{r.CodigoPedido}: {r.Codigo} — {r.Mensaje}");
        }

        resultados.Count(r => r.Exito).Should().Be(1);
        resultados.Single(r => !r.Exito).Codigo.Should().BeOneOf("STOCK_INSUFICIENTE", "CONFLICTO_CONCURRENCIA");
        (await e.StockAsync("P04")).Should().Be(0);
        (await e.ConsultarBdAsync(db => db.Pagos.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task P37_falla_del_notificador_deja_el_evento_pendiente_y_se_recupera_una_sola_vez()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);
        var demo = e.Servicios.GetRequiredService<IModoDemostracion>();
        demo.FallarNotificaciones = true;

        var id = await e.PedidoPagadoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        var pendiente = await e.ConsultarBdAsync(db => db.Outbox.SingleAsync());
        pendiente.Estado.Should().Be(EstadoOutbox.Pendiente);
        pendiente.UltimoError.Should().Contain("falla inducida");
        (await e.ConsultarBdAsync(db => db.Notificaciones.CountAsync())).Should().Be(0);

        demo.FallarNotificaciones = false;
        await e.ReiniciarAsync(); // reiniciar el procesador (nuevo contenedor)
        e.IniciarSesion(Rol.Ventas);
        var primera = await e.EnAmbitoAsync(sp => sp.GetRequiredService<ProcesadorOutbox>().ProcesarPendientesAsync());
        var segunda = await e.EnAmbitoAsync(sp => sp.GetRequiredService<ProcesadorOutbox>().ProcesarPendientesAsync());

        primera.Procesados.Should().Be(1);
        segunda.Procesados.Should().Be(0);
        var evento = await e.ConsultarBdAsync(db => db.Outbox.SingleAsync());
        evento.Estado.Should().Be(EstadoOutbox.Procesado);
        evento.ObservadoresCompletados.Split(';').Should().BeEquivalentTo("NOTIFICACION", "AUDITORIA", "METRICAS");
        (await e.ConsultarBdAsync(db => db.Notificaciones.CountAsync())).Should().Be(1);
        (await e.ConsultarBdAsync(db => db.Auditoria.CountAsync(a => a.Evento == "EVENTO PedidoPagado"))).Should().Be(1);
        (await e.ConsultarBdAsync(db => db.Pagos.CountAsync(p => p.PedidoId == id))).Should().Be(1, "recuperar el evento no repite el cobro");
    }
}
