using FluentAssertions;
using LogiMarket.Domain.Catalogo.Composite;
using LogiMarket.Domain.Catalogo.Visitantes;
using LogiMarket.Tests.Soporte;

namespace LogiMarket.Tests.Dominio;

/// <summary>P27 — Iterator y P34 — Visitor sobre KIT_TELETRABAJO.</summary>
public class IteratorVisitorTests
{
    private readonly IElementoCatalogo _teletrabajo = new CatalogoOficial().Arbol().Obtener("KIT_TELETRABAJO");

    [Fact]
    public void P27_iterador_entrega_cada_sku_una_vez_por_unidad_y_termina()
    {
        var iterador = _teletrabajo.CrearIterador();
        var recorrido = new List<string>();
        while (iterador.HaySiguiente())
        {
            recorrido.Add(iterador.Siguiente().Sku);
            recorrido.Count.Should().BeLessThan(100, "el recorrido debe terminar");
        }

        recorrido.Should().Equal("P01", "P02", "P03");
        iterador.HaySiguiente().Should().BeFalse();
        iterador.Invoking(i => i.Siguiente()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void P27_iterador_propaga_cantidades_de_forma_determinista()
    {
        static List<string> Recorrer(IElementoCatalogo e)
        {
            var it = e.CrearIterador(3);
            var lista = new List<string>();
            while (it.HaySiguiente())
            {
                lista.Add(it.Siguiente().Sku);
            }

            return lista;
        }

        var primero = Recorrer(_teletrabajo);

        primero.Should().HaveCount(9);
        primero.Count(s => s == "P02").Should().Be(3);
        Recorrer(_teletrabajo).Should().Equal(primero);
    }

    [Fact]
    public void P34_visitante_de_desglose_enumera_skus_y_visita_kits_anidados()
    {
        var visitante = new DesgloseSkuVisitor();

        _teletrabajo.Aceptar(visitante);

        visitante.Lineas.Select(l => (l.Sku, l.Cantidad)).Should().Equal(("P01", 1), ("P02", 1), ("P03", 1));
        visitante.KitsVisitados.Should().Equal("KIT_TELETRABAJO", "KIT_OFICINA");
    }

    [Fact]
    public void P34_visitante_de_peso_calcula_un_kilo_para_kit_teletrabajo()
    {
        var visitante = new PesoLogisticoVisitor();

        _teletrabajo.Aceptar(visitante);

        visitante.TotalKg.Should().Be(1.00m);
        visitante.ProductosVisitados.Should().Be(3);
    }
}
