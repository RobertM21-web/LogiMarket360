using FluentAssertions;
using LogiMarket.Domain.Catalogo.Composite;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Pedidos.Borrador;
using LogiMarket.Tests.Soporte;
using static LogiMarket.Tests.Soporte.DatosDominio;

namespace LogiMarket.Tests.Dominio;

/// <summary>P14 — Prototype de plantilla de carrito y P29 — Memento de ediciones del borrador.</summary>
public class BorradorMementoPrototypeTests
{
    private readonly CatalogoOficial _catalogo = new();

    private IElementoCatalogo Elemento(string codigo) => _catalogo.Arbol().Obtener(codigo);

    [Fact]
    public void P14_clonar_plantilla_produce_copia_profunda_independiente()
    {
        var plantilla = new PedidoBorrador();
        plantilla.SeleccionarCliente(Ana());
        plantilla.AgregarLinea(Elemento("KIT_TELETRABAJO"), 1);
        plantilla.AsociarPedido(99);

        var copia1 = plantilla.Clonar();
        var copia2 = plantilla.Clonar();
        copia1.CambiarCantidad("KIT_TELETRABAJO", 5);

        plantilla.Lineas.Single().Cantidad.Should().Be(1);
        copia2.Lineas.Single().Cantidad.Should().Be(1);
        copia1.Lineas.Single().Cantidad.Should().Be(5);
        copia1.PedidoId.Should().BeNull("la copia no hereda el id del pedido persistido");
        copia1.Lineas.Should().NotBeSameAs(plantilla.Lineas);
        copia1.Lineas.Single().Elemento.Should().NotBeSameAs(plantilla.Lineas.Single().Elemento);
        var kitCopia = (KitCompuesto)copia1.Lineas.Single().Elemento;
        var kitOriginal = (KitCompuesto)plantilla.Lineas.Single().Elemento;
        kitCopia.Componentes[0].Elemento.Should().NotBeSameAs(kitOriginal.Componentes[0].Elemento, "la estructura anidada también se copia");
    }

    [Fact]
    public void P14_clon_de_borrador_bloqueado_vuelve_a_ser_editable()
    {
        var original = new PedidoBorrador();
        original.AgregarLinea(Elemento("P01"), 1);
        original.Bloquear();

        var copia = original.Clonar();
        copia.AgregarLinea(Elemento("P02"), 1);

        copia.Lineas.Should().HaveCount(2);
        original.Lineas.Should().HaveCount(1);
    }

    [Fact]
    public void P29_tres_ediciones_dos_deshacer_un_rehacer_restauran_estados_exactos()
    {
        var borrador = new PedidoBorrador();
        borrador.SeleccionarCliente(Ana());
        var historial = new HistorialBorrador();

        historial.Ejecutar(borrador, "Añadir kit", b => b.AgregarLinea(Elemento("KIT_TELETRABAJO"), 1));
        historial.Ejecutar(borrador, "Añadir P03", b => b.AgregarLinea(Elemento("P03"), 2));
        historial.Ejecutar(borrador, "Cambiar cantidad", b => b.CambiarCantidad("KIT_TELETRABAJO", 3));

        historial.Deshacer(borrador);
        historial.Deshacer(borrador);
        borrador.Lineas.Select(l => (l.Elemento.Codigo, l.Cantidad)).Should().Equal(("KIT_TELETRABAJO", 1));

        historial.Rehacer(borrador);
        borrador.Lineas.Select(l => (l.Elemento.Codigo, l.Cantidad)).Should().Equal(("KIT_TELETRABAJO", 1), ("P03", 2));
        borrador.Lineas[0].Elemento.Should().BeOfType<KitCompuesto>().Which.Componentes.Should().HaveCount(2, "se restaura el ítem anidado completo");
    }

    [Fact]
    public void P29_historial_conserva_como_maximo_cinco_estados()
    {
        var borrador = new PedidoBorrador();
        var historial = new HistorialBorrador();

        for (var i = 1; i <= 7; i++)
        {
            historial.Ejecutar(borrador, $"Edición {i}", b => b.AgregarLinea(Elemento("P05"), 1));
        }

        historial.EstadosGuardados.Should().Be(HistorialBorrador.Capacidad);
        for (var i = 0; i < 5; i++)
        {
            historial.Deshacer(borrador);
        }

        borrador.Lineas.Single().Cantidad.Should().Be(2, "solo se pueden deshacer las últimas cinco ediciones");
        historial.PuedeDeshacer.Should().BeFalse();
    }

    [Fact]
    public void P29_al_confirmar_el_historial_se_limpia_y_no_permite_deshacer()
    {
        var borrador = new PedidoBorrador();
        var historial = new HistorialBorrador();
        historial.Ejecutar(borrador, "Añadir", b => b.AgregarLinea(Elemento("P01"), 1));

        historial.LimpiarYBloquear();
        borrador.Bloquear();

        historial.PuedeDeshacer.Should().BeFalse();
        historial.Invoking(h => h.Deshacer(borrador)).Should().Throw<ReglaNegocioException>();
        borrador.Invoking(b => b.AgregarLinea(Elemento("P02"), 1)).Should().Throw<ReglaNegocioException>().Which.Codigo.Should().Be("BORRADOR_BLOQUEADO");
    }
}
