using FluentAssertions;
using LogiMarket.Application.Consultas.Filtro;

namespace LogiMarket.Tests.Aplicacion;

/// <summary>P25 y P26 — Interpreter del filtro de pedidos.</summary>
public class InterpreterFiltroTests
{
    private sealed record P(string Codigo, string Estado, decimal Total, string Cliente) : IPedidoFiltrable;

    private static readonly P[] Pedidos =
    [
        new("PED-001", "PAGADO", 59.00m, "Ana López"),
        new("PED-002", "PAGADO", 102.00m, "Luis Ruiz"),
        new("PED-003", "VALIDADO", 90.00m, "Carmen Vega"),
        new("PED-004", "PAGADO", 20.00m, "Sofía Díaz"),
        new("PED-005", "ENTREGADO", 45.00m, "Ana López"),
    ];

    private static string[] Codigos(string filtro) => InterpreteFiltro.Filtrar(Pedidos, filtro).Select(p => p.Codigo).ToArray();

    [Fact]
    public void P25_estado_pagado_and_total_mayor_igual_50_devuelve_solo_los_correctos()
    {
        Codigos("ESTADO = PAGADO AND TOTAL >= 50").Should().Equal("PED-001", "PED-002");
    }

    [Fact]
    public void P25_cliente_con_comillas_y_minusculas()
    {
        Codigos("cliente = \"ana\" and estado = entregado").Should().Equal("PED-005");
    }

    [Fact]
    public void P26_and_tiene_mayor_precedencia_que_or()
    {
        // Sin paréntesis: VALIDADO OR (PAGADO AND TOTAL < 30)
        Codigos("ESTADO = VALIDADO OR ESTADO = PAGADO AND TOTAL < 30").Should().Equal("PED-003", "PED-004");
    }

    [Fact]
    public void P26_parentesis_cambian_la_precedencia()
    {
        // (VALIDADO OR PAGADO) AND TOTAL < 30
        Codigos("(ESTADO = VALIDADO OR ESTADO = PAGADO) AND TOTAL < 30").Should().Equal("PED-004");
        InterpreteFiltro.Analizar("(ESTADO = VALIDADO OR ESTADO = PAGADO) AND TOTAL < 30").Describir()
            .Should().Be("((ESTADO = VALIDADO OR ESTADO = PAGADO) AND TOTAL < 30.00)");
    }

    [Theory]
    [InlineData("ESTADO = PAGADO AND", "posición 20")]
    [InlineData("(ESTADO = PAGADO", "falta ')'")]
    [InlineData("TOTAL >= abc", "se esperaba un número")]
    [InlineData("PRECIO = 5", "campo desconocido 'PRECIO'")]
    [InlineData("ESTADO = PERDIDO", "estado desconocido")]
    [InlineData("ESTADO >= PAGADO", "solo admite el operador '='")]
    [InlineData("TOTAL >= 50; DROP TABLE Pedidos", "carácter no reconocido ';'")]
    public void P26_sintaxis_invalida_produce_error_legible(string filtro, string fragmento)
    {
        var accion = () => InterpreteFiltro.Analizar(filtro);

        accion.Should().Throw<FiltroInvalidoException>().Which.Message.Should().Contain(fragmento);
    }
}
