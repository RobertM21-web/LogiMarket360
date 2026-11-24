using System.Globalization;
using FluentAssertions;
using LogiMarket.Application.Consultas.Filtro;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Pagos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Pedidos.Estados;
using LogiMarket.Domain.Seguridad;
using LogiMarket.Tests.Soporte;

namespace LogiMarket.Tests.Calidad;

/// <summary>Pruebas de guarda de las refactorizaciones R04–R10 (docs/RIESGOS_REFACTORIZACION.md).</summary>
public class RefactorizacionTests
{
    private sealed record P(string Codigo, string Estado, decimal Total, string Cliente) : IPedidoFiltrable;

    private sealed class PasarelaEspia : IPasarelaPago
    {
        public Task<ResultadoCobro> CobrarAsync(decimal monto, string clave, CancellationToken ct = default) =>
            Task.FromResult(new ResultadoCobro(true, "ESPIA", "REF-1", "ok"));
    }

    [Fact]
    public void R04_los_nombres_de_estado_expuestos_coinciden_con_los_objetos_State_del_dominio()
    {
        NombresEstado.Todos.Should().Equal(Enum.GetValues<CodigoEstadoPedido>().Select(c => EstadoPedido.De(c).Nombre));
        NombresEstado.Flujo.Should().NotContain(NombresEstado.Cancelado);
        NombresEstado.Activos.Should().NotContain([NombresEstado.Entregado, NombresEstado.Cancelado]);
    }

    [Fact]
    public void R04_el_interprete_acepta_exactamente_los_estados_del_dominio()
    {
        var pedidos = NombresEstado.Todos.Select((e, i) => new P($"PED-{i}", e, 10m, "X")).ToArray();
        foreach (var estado in NombresEstado.Todos)
        {
            InterpreteFiltro.Filtrar(pedidos, $"ESTADO = {estado}").Should().ContainSingle().Which.Estado.Should().Be(estado);
        }

        var invalido = () => InterpreteFiltro.Filtrar(pedidos, "ESTADO = ARCHIVADO");
        invalido.Should().Throw<FiltroInvalidoException>().WithMessage("*Válidos: BORRADOR, VALIDADO, PAGADO, DESPACHADO, ENTREGADO, CANCELADO*");
    }

    [Fact]
    public async Task R05_los_importes_en_auditoria_usan_punto_decimal_aunque_la_cultura_sea_es_EC()
    {
        var cultura = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-EC");
        try
        {
            var auditoria = new AuditoriaEnMemoria();
            var proxy = new PagoAutorizadoProxy(new PasarelaEspia(), new Autorizador(Sesiones.Con(Rol.Consulta), auditoria), new MetricasSesion());

            var cobro = () => proxy.CobrarAsync(59.00m, "PED-001-K");

            await cobro.Should().ThrowAsync<AccesoDenegadoException>();
            auditoria.Registros.Should().ContainSingle().Which.Detalle.Should().Contain("USD 59.00").And.NotContain("59,00");
        }
        finally
        {
            CultureInfo.CurrentCulture = cultura;
        }
    }
}
