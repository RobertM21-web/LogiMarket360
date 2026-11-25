using FluentAssertions;
using LogiMarket.Application.Pedidos.Validacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Pedidos.Borrador;
using LogiMarket.Domain.Seguridad;
using LogiMarket.Tests.Soporte;

namespace LogiMarket.Tests.Aplicacion;

/// <summary>P23 — Chain of Responsibility: el primer error detiene la cadena y al corregirlo se alcanza el siguiente.</summary>
public class ChainValidacionTests
{
    private readonly CatalogoOficial _catalogo = new();

    private CadenaValidacionPedido Cadena(Rol rol) =>
        new(new ProductosEnMemoria(_catalogo.Productos), new Autorizador(Sesiones.Con(rol), new AuditoriaEnMemoria()));

    [Fact]
    public async Task P23_sin_cliente_y_sin_stock_se_detiene_en_el_primer_eslabon_y_luego_en_stock()
    {
        var borrador = new PedidoBorrador();
        borrador.AgregarLinea(_catalogo.Arbol().Obtener("KIT_TELETRABAJO"), 6); // requiere 6 P02, hay 5
        var cadena = Cadena(Rol.Ventas);

        var primero = await cadena.ValidarAsync(SolicitudValidacion.DesdeBorrador(borrador));

        primero.EsValido.Should().BeFalse();
        primero.Rechazo!.Codigo.Should().Be("PEDIDO_SIN_CLIENTE");
        primero.Recorrido.Should().Equal("Cliente activo");

        borrador.SeleccionarCliente(DatosDominio.Ana());
        var segundo = await cadena.ValidarAsync(SolicitudValidacion.DesdeBorrador(borrador));

        segundo.Rechazo!.Codigo.Should().Be("STOCK_INSUFICIENTE");
        segundo.Rechazo.Mensaje.Should().Contain("P02").And.Contain("6").And.Contain("5");
        segundo.Recorrido.Should().Equal("Cliente activo", "Líneas y cantidades", "Reglas de stock");
        _catalogo.P02.Stock.Should().Be(5, "la validación no modifica existencias");
    }

    [Fact]
    public async Task P23_pedido_valido_recorre_los_cuatro_eslabones()
    {
        var borrador = new PedidoBorrador();
        borrador.SeleccionarCliente(DatosDominio.Ana());
        borrador.AgregarLinea(_catalogo.Arbol().Obtener("KIT_TELETRABAJO"), 1);

        var r = await Cadena(Rol.Ventas).ValidarAsync(SolicitudValidacion.DesdeBorrador(borrador));

        r.EsValido.Should().BeTrue();
        r.Recorrido.Should().HaveCount(4).And.EndWith("Autorización y condiciones");
    }

    [Fact]
    public async Task P23_rol_sin_permiso_es_rechazado_en_el_ultimo_eslabon()
    {
        var borrador = new PedidoBorrador();
        borrador.SeleccionarCliente(DatosDominio.Ana());
        borrador.AgregarLinea(_catalogo.Arbol().Obtener("P01"), 1);

        var r = await Cadena(Rol.Bodega).ValidarAsync(SolicitudValidacion.DesdeBorrador(borrador));

        r.Rechazo!.Codigo.Should().Be("ACCESO_DENEGADO");
        r.Rechazo.Eslabon.Should().Be("Autorización y condiciones");
    }

    [Fact]
    public async Task P23_carrito_vacio_es_rechazado_en_lineas()
    {
        var borrador = new PedidoBorrador();
        borrador.SeleccionarCliente(DatosDominio.Luis());

        var r = await Cadena(Rol.Admin).ValidarAsync(SolicitudValidacion.DesdeBorrador(borrador));

        r.Rechazo!.Codigo.Should().Be("PEDIDO_SIN_LINEAS");
        r.Recorrido.Should().HaveCount(2);
    }
}
