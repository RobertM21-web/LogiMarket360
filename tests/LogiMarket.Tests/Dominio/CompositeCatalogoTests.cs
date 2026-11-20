using FluentAssertions;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Catalogo.Composite;
using LogiMarket.Domain.Catalogo.Recorrido;
using LogiMarket.Domain.Comun;
using LogiMarket.Tests.Soporte;

namespace LogiMarket.Tests.Dominio;

/// <summary>P18 — Composite: precios recursivos, expansión a SKU finales y rechazo de ciclos.</summary>
public class CompositeCatalogoTests
{
    private readonly CatalogoOficial _catalogo = new();

    [Fact]
    public void P18_precios_de_kits_se_calculan_recursivamente()
    {
        var arbol = _catalogo.Arbol();

        arbol.Obtener("KIT_OFICINA").Precio.Should().Be(Dinero.Usd(50.00m));
        arbol.Obtener("KIT_TELETRABAJO").Precio.Should().Be(Dinero.Usd(60.00m));
    }

    [Fact]
    public void P18_kit_anidado_se_expande_hasta_los_sku_finales()
    {
        var expansion = ExpansionSku.Expandir(_catalogo.Arbol().Obtener("KIT_TELETRABAJO"), 2);

        expansion.Select(r => (r.Producto.Sku, r.Cantidad))
            .Should().Equal(("P01", 2), ("P02", 2), ("P03", 2));
    }

    [Fact]
    public void P18_ciclo_indirecto_es_rechazado_sin_modificar_la_composicion()
    {
        // KIT_OFICINA contendría a KIT_TELETRABAJO, que ya contiene a KIT_OFICINA.
        var accion = () => ArbolCatalogo.RedefinirKit(
            _catalogo.KitOficina,
            [ComposicionPropuesta.DeKit(_catalogo.KitTeletrabajo.Id, 1)],
            _catalogo.Productos,
            _catalogo.Kits);

        accion.Should().Throw<ReglaNegocioException>().Which.Codigo.Should().Be("KIT_CICLICO");
        _catalogo.KitOficina.Componentes.Select(c => c.ProductoId).Should().Equal(1, 2);
    }

    [Fact]
    public void P18_kit_no_puede_contenerse_a_si_mismo()
    {
        var kit = new KitCompuesto(9, "KIT_X", "Kit X");

        var accion = () => kit.Agregar(kit, 1);

        accion.Should().Throw<ReglaNegocioException>().Which.Codigo.Should().Be("KIT_CICLICO");
    }
}
