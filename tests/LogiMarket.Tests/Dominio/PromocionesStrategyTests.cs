using FluentAssertions;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Promociones;
using LogiMarket.Tests.Soporte;

namespace LogiMarket.Tests.Dominio;

/// <summary>P32 — Strategy y verificación de los cálculos oficiales (Ana, Luis, Carmen).</summary>
public class PromocionesStrategyTests
{
    private readonly CatalogoOficial _catalogo = new();
    private readonly CalculadoraPedido _calculadora = new(SelectorPoliticaDescuento.Oficial());
    private static readonly Dinero Estandar = new FabricaEntregaEstandar().CrearTarifa().Calcular();
    private static readonly Dinero Expres = new FabricaEntregaExpres().CrearTarifa().Calcular();

    private LineaValoracion Linea(string codigo, int cantidad) => new(_catalogo.Arbol().Obtener(codigo), cantidad);

    [Fact]
    public void P32_ana_vip_recibe_10_por_ciento_total_59()
    {
        var r = _calculadora.Calcular(CategoriaCliente.Vip, [Linea("KIT_TELETRABAJO", 1)], Estandar);

        r.Politica.Should().Be("VIP");
        r.Subtotal.Should().Be(Dinero.Usd(60.00m));
        r.Descuento.Should().Be(Dinero.Usd(6.00m));
        r.Total.Should().Be(Dinero.Usd(59.00m));
    }

    [Fact]
    public void P32_luis_normal_sin_descuento_total_102()
    {
        var r = _calculadora.Calcular(CategoriaCliente.Normal, [Linea("P04", 2), Linea("P05", 2)], Expres);

        r.Politica.Should().Be("NORMAL");
        r.Descuento.Should().Be(Dinero.Cero);
        r.Total.Should().Be(Dinero.Usd(102.00m));
    }

    [Fact]
    public void P32_carmen_normal_recibe_mayorista_por_volumen_total_90()
    {
        var r = _calculadora.Calcular(CategoriaCliente.Normal, [Linea("P03", 10)], Estandar);

        r.Politica.Should().Be("MAYORISTA");
        r.Descuento.Should().Be(Dinero.Usd(15.00m));
        r.Total.Should().Be(Dinero.Usd(90.00m));
    }

    [Fact]
    public void P32_vip_con_volumen_mayorista_no_acumula_y_prioriza_mayorista()
    {
        // 4 kits de teletrabajo = 12 unidades físicas finales.
        var r = _calculadora.Calcular(CategoriaCliente.Vip, [Linea("KIT_TELETRABAJO", 4)], Estandar);

        r.UnidadesFisicas.Should().Be(12);
        r.Politica.Should().Be("MAYORISTA");
        r.Descuento.Should().Be(Dinero.Usd(36.00m), "solo 15 % de 240.00, nunca 25 %");
    }

    [Fact]
    public void P32_nueva_promocion_se_agrega_sin_modificar_la_calculadora()
    {
        var selector = new SelectorPoliticaDescuento(
        [
            new PoliticaMayorista15(), new PoliticaVip10(), new PoliticaSinDescuento(), new PoliticaPruebaKitDoble(),
        ]);
        var calculadora = new CalculadoraPedido(selector);

        var r = calculadora.Calcular(CategoriaCliente.Normal, [Linea("KIT_OFICINA", 2)], Estandar);

        r.Politica.Should().Be("PRUEBA_5");
        r.Descuento.Should().Be(Dinero.Usd(5.00m));
    }

    [Fact]
    public void Descuento_se_redondea_away_from_zero()
    {
        Dinero.Usd(0.125m).Redondeado().Should().Be(Dinero.Usd(0.13m));
        Dinero.Usd(33.35m).Por(0.15m).Should().Be(Dinero.Usd(5.00m)); // 5.0025 → 5.00
        Dinero.Usd(10.10m).Por(0.15m).Should().Be(Dinero.Usd(1.52m)); // 1.515 → 1.52
    }

    /// <summary>Política de prueba: 5 % a partir de 4 unidades físicas (prioridad entre VIP y NORMAL).</summary>
    private sealed class PoliticaPruebaKitDoble : IPoliticaDescuento
    {
        public string Codigo => "PRUEBA_5";

        public int Prioridad => 5;

        public decimal Tasa => 0.05m;

        public bool Aplica(ContextoDescuento contexto) => contexto.UnidadesFisicas >= 4;
    }
}
