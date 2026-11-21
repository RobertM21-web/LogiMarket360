using FluentAssertions;
using LogiMarket.Application.Comandos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Pagos;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Seguridad;
using LogiMarket.Tests.Soporte;

namespace LogiMarket.Tests.Aplicacion;

/// <summary>P24 — Command con invocador y P22 — Proxy de autorización de pagos (sin WinForms).</summary>
public class CommandProxyTests
{
    private sealed class ComandoEspia : IComandoNegocio
    {
        public int Ejecuciones { get; private set; }

        public string Nombre => "Espia";

        public string Entrada => "x=1";

        public Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct)
        {
            Ejecuciones++;
            return Ejecuciones == 1
                ? Task.FromResult(ResultadoOperacion.Ok("hecho"))
                : throw new ReglaNegocioException("REPETIDO", "Ya ejecutado");
        }
    }

    private sealed class PasarelaEspia : IPasarelaPago
    {
        public int Llamadas { get; private set; }

        public Task<ResultadoCobro> CobrarAsync(decimal monto, string clave, CancellationToken ct = default)
        {
            Llamadas++;
            return Task.FromResult(new ResultadoCobro(true, "ESPIA", $"REF-{Llamadas}", "ok"));
        }
    }

    [Fact]
    public async Task P24_invocador_ejecuta_y_registra_entrada_y_resultado_incluidos_errores()
    {
        var auditoria = new AuditoriaEnMemoria();
        var invocador = new InvocadorComandos(Sesiones.Con(Rol.Ventas, "ventas.demo"), auditoria, new MetricasSesion(), new RelojFijo(DatosDominio.Ahora));
        var comando = new ComandoEspia();

        var r1 = await invocador.EjecutarAsync(comando);
        var r2 = await invocador.EjecutarAsync(comando);

        r1.Exito.Should().BeTrue();
        r2.Exito.Should().BeFalse();
        r2.Codigo.Should().Be("REPETIDO");
        invocador.Historial.Select(h => (h.Comando, h.Entrada, h.Exito, h.Usuario))
            .Should().Equal(("Espia", "x=1", true, "ventas.demo"), ("Espia", "x=1", false, "ventas.demo"));
        auditoria.Registros.Should().HaveCount(2).And.OnlyContain(a => a.Categoria == "COMANDO");
    }

    [Theory]
    [InlineData(Rol.Consulta)]
    [InlineData(Rol.Bodega)]
    public async Task P22_roles_sin_permiso_no_alcanzan_la_pasarela(Rol rol)
    {
        var real = new PasarelaEspia();
        var auditoria = new AuditoriaEnMemoria();
        var metricas = new MetricasSesion();
        var proxy = new PagoAutorizadoProxy(real, new Autorizador(Sesiones.Con(rol), auditoria), metricas);

        var accion = () => proxy.CobrarAsync(59m, "clave");

        await accion.Should().ThrowAsync<AccesoDenegadoException>();
        real.Llamadas.Should().Be(0);
        auditoria.Registros.Should().ContainSingle(a => a.Categoria == "SEGURIDAD" && a.Resultado == "DENEGADO");
        metricas.Instantanea.CobrosDenegados.Should().Be(1);
    }

    [Theory]
    [InlineData(Rol.Ventas)]
    [InlineData(Rol.Admin)]
    public async Task P22_ventas_y_admin_son_autorizados(Rol rol)
    {
        var real = new PasarelaEspia();
        var proxy = new PagoAutorizadoProxy(real, new Autorizador(Sesiones.Con(rol), new AuditoriaEnMemoria()), new MetricasSesion());

        var r = await proxy.CobrarAsync(59m, "clave");

        r.Aprobado.Should().BeTrue();
        real.Llamadas.Should().Be(1);
    }
}
