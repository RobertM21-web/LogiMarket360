using FluentAssertions;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pedidos.Estados;
using LogiMarket.Domain.Seguridad;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.Tests.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Tests.Integracion;

/// <summary>Grupo A — P01 a P10 sobre SQLite real en archivo temporal.</summary>
public class FuncionalidadReglasTests
{
    [Fact]
    public async Task P01_bd_desde_archivo_inexistente_migra_siembra_y_no_duplica_al_relanzar()
    {
        await using var e = await EntornoIntegracion.CrearAsync(inicializar: false);
        File.Exists(e.Opciones.RutaBaseDatos).Should().BeFalse();

        var primera = await e.EnAmbitoAsync(sp => sp.GetRequiredService<InicializadorBaseDatos>().InicializarAsync());
        var segunda = await e.EnAmbitoAsync(sp => sp.GetRequiredService<InicializadorBaseDatos>().InicializarAsync());

        File.Exists(e.Opciones.RutaBaseDatos).Should().BeTrue();
        primera.Should().Be(5 + 2 + 4 + 4);
        segunda.Should().Be(0, "la semilla es idempotente");
        var (productos, kits, usuarios, clientes, migraciones, hashes) = await e.ConsultarBdAsync(async db => (
            await db.Productos.CountAsync(), await db.Kits.CountAsync(), await db.Usuarios.CountAsync(), await db.Clientes.CountAsync(),
            (await db.Database.GetAppliedMigrationsAsync()).ToList(), await db.Usuarios.Select(u => u.HashContrasena).ToListAsync()));
        (productos, kits, usuarios, clientes).Should().Be((5, 2, 4, 4));
        migraciones.Should().ContainSingle(m => m.EndsWith("_Inicial"));
        hashes.Should().NotContain(h => SemillaDatos.CuentasDemo.Any(c => h.Contains(c.Contrasena)), "las contraseñas nunca se guardan en claro");
        (await e.StockAsync("P01")).Should().Be(8);
    }

    [Fact]
    public async Task P02_ana_vip_kit_teletrabajo_estandar_total_59_y_stock_7_4_19()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);

        var id = await e.PedidoPagadoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));

        var pedido = await e.ConsultarBdAsync(db => db.Pedidos.Include(p => p.Pago).SingleAsync(p => p.Id == id));
        pedido.Total.Valor.Should().Be(59.00m);
        pedido.Descuento.Valor.Should().Be(6.00m);
        pedido.CodigoEstado.Should().Be(CodigoEstadoPedido.Pagado);
        (await e.ConsultarBdAsync(db => db.Pagos.CountAsync(p => p.PedidoId == id))).Should().Be(1);
        (await e.StockAsync("P01"), await e.StockAsync("P02"), await e.StockAsync("P03")).Should().Be((7, 4, 19));
    }

    [Fact]
    public async Task P03_luis_normal_expres_total_102()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);

        var id = await e.PedidoPagadoAsync("C002", TipoEntrega.Expres, ("P04", 2), ("P05", 2));

        (await e.ConsultarBdAsync(db => db.Pedidos.Where(p => p.Id == id).Select(p => p.Total).SingleAsync())).Valor.Should().Be(102.00m);
        (await e.StockAsync("P04"), await e.StockAsync("P05")).Should().Be((2, 28));
        (await e.ConsultarBdAsync(db => db.Pagos.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task P04_carmen_mayorista_por_volumen_total_90()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);

        var id = await e.PedidoPagadoAsync("C004", TipoEntrega.Estandar, ("P03", 10));

        var pedido = await e.ConsultarBdAsync(db => db.Pedidos.SingleAsync(p => p.Id == id));
        (pedido.Total.Valor, pedido.Descuento.Valor, pedido.PoliticaDescuento).Should().Be((90.00m, 15.00m, "MAYORISTA"));
        (await e.StockAsync("P03")).Should().Be(10);
    }

    [Fact]
    public async Task P05_kit_teletrabajo_por_6_se_rechaza_por_p02_sin_efectos()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);

        var r = await e.ValidarPedidoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 6));

        r.Exito.Should().BeFalse();
        r.Codigo.Should().Be("STOCK_INSUFICIENTE");
        r.Mensaje.Should().Contain("P02");
        (await e.ConsultarBdAsync(db => db.Pedidos.CountAsync())).Should().Be(0);
        (await e.ConsultarBdAsync(db => db.Pagos.CountAsync())).Should().Be(0);
        (await e.ConsultarBdAsync(db => db.Outbox.CountAsync())).Should().Be(0);
        (await e.ConsultarBdAsync(db => db.Movimientos.CountAsync(m => m.Motivo == MotivoMovimiento.Venta))).Should().Be(0);
        (await e.StockAsync("P02")).Should().Be(5);
    }

    [Fact]
    public async Task P05b_stock_consumido_entre_validacion_y_confirmacion_se_rechaza_sin_cobro()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);
        var primero = await e.ValidarPedidoAsync("C002", TipoEntrega.Estandar, ("P04", 3));
        var segundo = await e.ValidarPedidoAsync("C003", TipoEntrega.Estandar, ("P04", 3));
        (await e.ConfirmarAsync(primero.PedidoId!.Value)).Exito.Should().BeTrue();

        var r = await e.ConfirmarAsync(segundo.PedidoId!.Value);

        r.Exito.Should().BeFalse();
        r.Codigo.Should().Be("STOCK_INSUFICIENTE");
        (await e.ConsultarBdAsync(db => db.Pagos.CountAsync())).Should().Be(1);
        (await e.StockAsync("P04")).Should().Be(1);
    }

    [Fact]
    public async Task P06_error_de_cobro_simulado_deja_todo_intacto_sin_outbox_de_exito()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);
        var validado = await e.ValidarPedidoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        e.Servicios.GetRequiredService<IModoDemostracion>().RechazarCobros = true;

        var r = await e.ConfirmarAsync(validado.PedidoId!.Value);

        r.Exito.Should().BeFalse();
        r.Codigo.Should().Be("COBRO_RECHAZADO");
        var estado = await e.ConsultarBdAsync(db => db.Pedidos.Where(p => p.Id == validado.PedidoId).Select(p => p.CodigoEstado).SingleAsync());
        estado.Should().Be(CodigoEstadoPedido.Validado);
        (await e.ConsultarBdAsync(db => db.Pagos.CountAsync())).Should().Be(0);
        (await e.ConsultarBdAsync(db => db.Outbox.CountAsync())).Should().Be(0);
        (await e.StockAsync("P01"), await e.StockAsync("P02"), await e.StockAsync("P03")).Should().Be((8, 5, 20));
        (await e.ConsultarBdAsync(db => db.Auditoria.CountAsync(a => a.Evento == "COBRO RECHAZADO"))).Should().Be(1);
    }

    [Fact]
    public async Task P07_repetir_confirmacion_con_la_misma_clave_devuelve_el_resultado_original_sin_cobro_doble()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);
        var validado = await e.ValidarPedidoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));

        var primero = await e.EnAmbitoAsync(sp => sp.GetRequiredService<ConfirmacionPedidoFacade>().ConfirmarAsync(validado.PedidoId!.Value));
        var segundo = await e.EnAmbitoAsync(sp => sp.GetRequiredService<ConfirmacionPedidoFacade>().ConfirmarAsync(validado.PedidoId!.Value));

        primero.Exito.Should().BeTrue();
        segundo.EsRepeticion.Should().BeTrue();
        segundo.ReferenciaCobro.Should().Be(primero.ReferenciaCobro);
        segundo.Total.Should().Be(primero.Total);
        (await e.ConsultarBdAsync(db => db.Pagos.CountAsync())).Should().Be(1);
        var movimientos = await e.ConsultarBdAsync(db => db.Movimientos.Where(m => m.Motivo == MotivoMovimiento.Venta).GroupBy(m => m.Sku).Select(g => g.Count()).ToListAsync());
        movimientos.Should().HaveCount(3).And.OnlyContain(c => c == 1);
    }

    [Fact]
    public async Task P08_pedido_pagado_no_se_cancela_ni_se_modifica_y_conserva_su_precio()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Admin);
        var id = await e.PedidoPagadoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));

        var cancelar = await e.EnAmbitoAsync(sp => sp.GetRequiredService<Application.Comandos.InvocadorComandos>()
            .EjecutarAsync(sp.GetRequiredService<Application.Comandos.FabricaComandos>().Cancelar(id)));
        var editar = () => e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioPedidos>().AbrirBorradorAsync(id));
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<Application.Catalogo.ServicioCatalogo>()
            .ActualizarProductoAsync(1, new Application.Catalogo.DatosProducto("P01", "Mouse USB", "Periféricos", "", 99m, 0.2m, 0)));

        cancelar.Exito.Should().BeFalse();
        cancelar.Codigo.Should().Be("TRANSICION_INVALIDA");
        await editar.Should().ThrowAsync<Domain.Comun.ReglaNegocioException>().Where(x => x.Codigo == "TRANSICION_INVALIDA");
        var pedido = await e.ConsultarBdAsync(db => db.Pedidos.Include(p => p.Historial).SingleAsync(p => p.Id == id));
        pedido.CodigoEstado.Should().Be(CodigoEstadoPedido.Pagado);
        pedido.Total.Valor.Should().Be(59.00m, "la instantánea no cambia con el catálogo");
        pedido.Historial.Should().HaveCount(3);
    }

    [Fact]
    public async Task P09_bodega_despacha_y_entrega_en_secuencia_y_no_despacha_sin_pago()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);
        var sinPago = (await e.ValidarPedidoAsync("C003", TipoEntrega.Estandar, ("P05", 1))).PedidoId!.Value;
        var pagado = await e.PedidoPagadoAsync("C002", TipoEntrega.Expres, ("P04", 2), ("P05", 2));
        e.IniciarSesion(Rol.Bodega);

        var despacharSinPago = () => e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioLogistica>().DespacharAsync(sinPago));
        await despacharSinPago.Should().ThrowAsync<Domain.Comun.ReglaNegocioException>().Where(x => x.Codigo == "TRANSICION_INVALIDA");
        var cobrar = () => e.EnAmbitoAsync(sp => sp.GetRequiredService<ConfirmacionPedidoFacade>().ConfirmarAsync(sinPago));
        await cobrar.Should().ThrowAsync<AccesoDenegadoException>();

        (await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioLogistica>().DespacharAsync(pagado))).Mensaje.Should().Contain("GUIA-");
        (await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioLogistica>().EntregarAsync(pagado))).Exito.Should().BeTrue();

        var p = await e.ConsultarBdAsync(db => db.Pedidos.Include(x => x.Despacho).SingleAsync(x => x.Id == pagado));
        p.CodigoEstado.Should().Be(CodigoEstadoPedido.Entregado);
        p.Despacho!.TipoEntrega.Should().Be(TipoEntrega.Expres);
        p.Despacho.PlazoDias.Should().Be(1);
        (await e.ConsultarBdAsync(db => db.Pedidos.Where(x => x.Id == sinPago).Select(x => x.CodigoEstado).SingleAsync())).Should().Be(CodigoEstadoPedido.Validado);
    }

    [Fact]
    public async Task P10_cerrar_y_reabrir_conserva_pedido_stock_pago_y_auditoria()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);
        var id = await e.PedidoPagadoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        var auditoriaAntes = await e.ConsultarBdAsync(db => db.Auditoria.CountAsync());

        await e.ReiniciarAsync();

        var p = await e.ConsultarBdAsync(db => db.Pedidos.Include(x => x.Pago).SingleAsync(x => x.Id == id));
        p.CodigoEstado.Should().Be(CodigoEstadoPedido.Pagado);
        p.Pago!.Monto.Valor.Should().Be(59.00m);
        (await e.StockAsync("P02")).Should().Be(4);
        (await e.ConsultarBdAsync(db => db.Auditoria.CountAsync())).Should().BeGreaterThanOrEqualTo(auditoriaAntes);
        (await e.ConsultarBdAsync(db => db.Productos.CountAsync())).Should().Be(5, "relanzar la semilla tras reiniciar no duplica");
    }
}
