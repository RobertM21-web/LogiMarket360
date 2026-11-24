using FluentAssertions;
using LogiMarket.Application.Consultas.Filtro;
using LogiMarket.Domain.Pedidos.Estados;

namespace LogiMarket.Tests.Calidad;

/// <summary>Pruebas de guarda de las refactorizaciones R04–R10 (docs/RIESGOS_REFACTORIZACION.md).</summary>
public class RefactorizacionTests
{
    private sealed record P(string Codigo, string Estado, decimal Total, string Cliente) : IPedidoFiltrable;

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
}
