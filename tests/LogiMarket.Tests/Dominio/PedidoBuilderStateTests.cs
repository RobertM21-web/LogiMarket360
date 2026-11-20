using FluentAssertions;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Pedidos.Estados;
using LogiMarket.Tests.Soporte;
using static LogiMarket.Tests.Soporte.DatosDominio;

namespace LogiMarket.Tests.Dominio;

/// <summary>P13 — Builder y P31 — State del ciclo del pedido.</summary>
public class PedidoBuilderStateTests
{
    private readonly CatalogoOficial _catalogo = new();

    [Fact]
    public void P13_pedido_sin_cliente_es_rechazado()
    {
        var accion = () => Builder().Agregar(_catalogo.Arbol().Obtener("P01"), 1).Construir("PED-001", "ventas.demo", Ahora);

        accion.Should().Throw<ReglaNegocioException>().Which.Codigo.Should().Be("PEDIDO_SIN_CLIENTE");
    }

    [Fact]
    public void P13_pedido_sin_lineas_es_rechazado()
    {
        var accion = () => Builder().ParaCliente(Ana()).Construir("PED-001", "ventas.demo", Ahora);

        accion.Should().Throw<ReglaNegocioException>().Which.Codigo.Should().Be("PEDIDO_SIN_LINEAS");
    }

    [Fact]
    public void P13_pedido_completo_multirrenglon_crea_agregado_valido_con_instantaneas()
    {
        var arbol = _catalogo.Arbol();

        var pedido = Builder().ParaCliente(Luis())
            .Agregar(arbol.Obtener("P04"), 2)
            .Agregar(arbol.Obtener("P05"), 2)
            .Agregar(arbol.Obtener("KIT_OFICINA"), 1)
            .ConEntrega(TipoEntrega.Expres)
            .Construir("PED-002", "ventas.demo", Ahora);

        pedido.CodigoEstado.Should().Be(CodigoEstadoPedido.Borrador);
        pedido.Detalles.Should().HaveCount(3);
        pedido.Detalles.Single(d => d.Codigo == "KIT_OFICINA").Componentes.Select(c => c.Sku).Should().Equal("P01", "P02");
        pedido.Total.Should().Be(Dinero.Usd(80m + 10m + 50m + 12m));
        pedido.ClaveIdempotencia.Should().HaveLength(32);
    }

    [Fact]
    public void Instantanea_no_cambia_si_el_catalogo_cambia_despues()
    {
        var pedido = PedidoAna(_catalogo);

        _catalogo.P01.ActualizarDatos("Mouse USB", "Periféricos", "", Dinero.Usd(99m), 0.2m);

        pedido.Subtotal.Should().Be(Dinero.Usd(60m));
        pedido.Detalles.Single().PrecioUnitario.Should().Be(Dinero.Usd(60m));
    }

    [Fact]
    public void P31_ciclo_completo_valido_registra_historial_y_eventos()
    {
        var pedido = PedidoAna(_catalogo);
        var estandar = new FabricaEntregaEstandar();

        pedido.Validar("ventas.demo", Ahora);
        pedido.RegistrarPago("LEGACY", "LEG-0001", "ventas.demo", Ahora);
        pedido.Despachar(estandar.CrearConstructorGuia().Construir(pedido.Codigo, 1, Ahora, estandar.CrearPlazo()), "bodega.demo", Ahora);
        pedido.Entregar("bodega.demo", Ahora.AddDays(3));

        pedido.CodigoEstado.Should().Be(CodigoEstadoPedido.Entregado);
        pedido.Historial.Select(h => h.EstadoNuevo).Should().Equal(
            CodigoEstadoPedido.Borrador, CodigoEstadoPedido.Validado, CodigoEstadoPedido.Pagado,
            CodigoEstadoPedido.Despachado, CodigoEstadoPedido.Entregado);
        pedido.FechaEntregaUtc.Should().Be(Ahora.AddDays(3));
        pedido.Eventos.Select(e => e.GetType()).Should().Equal(typeof(PedidoPagado), typeof(DespachoRegistrado));
    }

    [Fact]
    public void P31_pagado_no_puede_cancelarse_ni_editarse_ni_cobrarse_otra_vez()
    {
        var pedido = PedidoAna(_catalogo);
        pedido.Validar("ventas.demo", Ahora);
        pedido.RegistrarPago("LEGACY", "LEG-0001", "ventas.demo", Ahora);
        var otroContenido = Builder().ParaCliente(Ana()).Agregar(_catalogo.Arbol().Obtener("P01"), 1).ConstruirContenido();

        pedido.Invoking(p => p.Cancelar("ventas.demo", Ahora))
            .Should().Throw<ReglaNegocioException>().Which.Codigo.Should().Be("TRANSICION_INVALIDA");
        pedido.Invoking(p => p.ReemplazarContenido(otroContenido))
            .Should().Throw<ReglaNegocioException>().Which.Codigo.Should().Be("TRANSICION_INVALIDA");
        pedido.Invoking(p => p.RegistrarPago("LEGACY", "LEG-0002", "ventas.demo", Ahora))
            .Should().Throw<ReglaNegocioException>().Which.Codigo.Should().Be("COBRO_DUPLICADO");
        pedido.CodigoEstado.Should().Be(CodigoEstadoPedido.Pagado);
        pedido.Total.Should().Be(Dinero.Usd(59m));
    }

    [Fact]
    public void P31_estados_repetidos_y_saltos_son_rechazados()
    {
        var pedido = PedidoAna(_catalogo);

        pedido.Invoking(p => p.RegistrarPago("LEGACY", "LEG-1", "v", Ahora)).Should().Throw<ReglaNegocioException>("no se cobra un borrador");
        pedido.Validar("ventas.demo", Ahora);
        pedido.Invoking(p => p.Validar("ventas.demo", Ahora)).Should().Throw<ReglaNegocioException>("estado repetido");
        pedido.Invoking(p => p.Entregar("bodega.demo", Ahora)).Should().Throw<ReglaNegocioException>("no se entrega sin despachar");
        pedido.Estado.PermiteDespachar.Should().BeFalse("no se despacha antes de pagar");
        pedido.CodigoEstado.Should().Be(CodigoEstadoPedido.Validado);
    }

    [Fact]
    public void P31_cancelacion_permitida_desde_borrador_y_validado()
    {
        var borrador = PedidoAna(_catalogo, 1);
        var validado = PedidoAna(_catalogo, 2);
        validado.Validar("ventas.demo", Ahora);

        borrador.Cancelar("ventas.demo", Ahora);
        validado.Cancelar("ventas.demo", Ahora);

        borrador.CodigoEstado.Should().Be(CodigoEstadoPedido.Cancelado);
        validado.CodigoEstado.Should().Be(CodigoEstadoPedido.Cancelado);
        validado.Invoking(p => p.Validar("v", Ahora)).Should().Throw<ReglaNegocioException>();
    }
}
