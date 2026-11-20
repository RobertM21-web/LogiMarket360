using FluentAssertions;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Entrega;

namespace LogiMarket.Tests.Dominio;

/// <summary>P12 — Abstract Factory de familias de entrega.</summary>
public class FamiliasEntregaTests
{
    private static readonly DateTime Despacho = new(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(TipoEntrega.Estandar, 5.00, 3, "ENVÍO ESTÁNDAR")]
    [InlineData(TipoEntrega.Expres, 12.00, 1, "ENVÍO EXPRÉS")]
    public void P12_cada_familia_produce_tarifa_plazo_y_guia_coherentes(TipoEntrega tipo, decimal tarifa, int dias, string etiqueta)
    {
        var fabrica = CatalogoFamiliasEntrega.Oficial().Obtener(tipo);

        var plazo = fabrica.CrearPlazo();
        var guia = fabrica.CrearConstructorGuia().Construir("PED-008", 8, Despacho, plazo);

        fabrica.CrearTarifa().Calcular().Should().Be(Dinero.Usd(tarifa));
        plazo.Dias.Should().Be(dias);
        guia.Numero.Should().Be("GUIA-2026-0008");
        guia.Familia.Should().Be(tipo);
        guia.Etiqueta.Should().StartWith(etiqueta);
        guia.FechaEstimadaUtc.Should().Be(Despacho.AddDays(dias));
    }

    [Fact]
    public void P12_no_se_pueden_mezclar_productos_de_familias_distintas()
    {
        var constructorEstandar = new FabricaEntregaEstandar().CrearConstructorGuia();
        var plazoExpres = new FabricaEntregaExpres().CrearPlazo();

        var accion = () => constructorEstandar.Construir("PED-001", 1, Despacho, plazoExpres);

        accion.Should().Throw<ReglaNegocioException>().Which.Codigo.Should().Be("FAMILIA_INCOMPATIBLE");
    }
}
