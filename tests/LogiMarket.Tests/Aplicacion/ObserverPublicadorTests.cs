using FluentAssertions;
using LogiMarket.Application.Eventos;
using LogiMarket.Domain.Eventos;
using LogiMarket.Tests.Soporte;

namespace LogiMarket.Tests.Aplicacion;

/// <summary>P30 — Observer: cada suscriptor atiende el evento una sola vez, incluso con reintentos.</summary>
public class ObserverPublicadorTests
{
    private sealed class ObservadorContador(string nombre, int fallosIniciales = 0) : IObservadorEventos
    {
        private int _fallosRestantes = fallosIniciales;

        public int Atendidos { get; private set; }

        public string Nombre => nombre;

        public bool Interesa(IEventoDominio evento) => evento is PedidoPagado;

        public Task ManejarAsync(IEventoDominio evento, CancellationToken ct)
        {
            if (_fallosRestantes-- > 0)
            {
                throw new InvalidOperationException($"{nombre} no disponible");
            }

            Atendidos++;
            return Task.CompletedTask;
        }
    }

    private static readonly PedidoPagado Evento = new(21, "PED-021", 1, 59m, "LEG-1", "ventas.demo", DatosDominio.Ahora);

    [Fact]
    public async Task P30_pedido_pagado_con_tres_suscriptores_se_atiende_una_vez_por_suscriptor()
    {
        var notificacion = new ObservadorContador("NOTIFICACION");
        var auditoria = new ObservadorContador("AUDITORIA");
        var metricas = new ObservadorContador("METRICAS");
        var publicador = new PublicadorEventos([notificacion, auditoria, metricas]);
        var registro = SerializadorEventos.AOutbox(Evento, "PED-021", DatosDominio.Ahora);
        var uow = new UnidadDeTrabajoContador();

        var ok = await publicador.PublicarAsync(registro, uow, new RelojFijo(DatosDominio.Ahora), default);
        var otraVez = await publicador.PublicarAsync(registro, uow, new RelojFijo(DatosDominio.Ahora), default);

        ok.Should().BeTrue();
        otraVez.Should().BeTrue();
        new[] { notificacion.Atendidos, auditoria.Atendidos, metricas.Atendidos }.Should().Equal(1, 1, 1);
        registro.Estado.Should().Be(Application.Registros.EstadoOutbox.Procesado);
    }

    [Fact]
    public async Task P30_si_un_suscriptor_falla_el_reintento_no_duplica_a_los_que_ya_atendieron()
    {
        var auditoria = new ObservadorContador("AUDITORIA");
        var notificacion = new ObservadorContador("NOTIFICACION", fallosIniciales: 1);
        var publicador = new PublicadorEventos([auditoria, notificacion]);
        var registro = SerializadorEventos.AOutbox(Evento, "PED-021", DatosDominio.Ahora);
        var uow = new UnidadDeTrabajoContador();
        var reloj = new RelojFijo(DatosDominio.Ahora);

        (await publicador.PublicarAsync(registro, uow, reloj, default)).Should().BeFalse();
        registro.Estado.Should().Be(Application.Registros.EstadoOutbox.Pendiente);
        registro.UltimoError.Should().Contain("NOTIFICACION");

        (await publicador.PublicarAsync(registro, uow, reloj, default)).Should().BeTrue();

        auditoria.Atendidos.Should().Be(1);
        notificacion.Atendidos.Should().Be(1);
        registro.Intentos.Should().Be(2);
    }

    [Fact]
    public void Serializador_reconstruye_el_evento_original()
    {
        var registro = SerializadorEventos.AOutbox(Evento, "PED-021", DatosDominio.Ahora);

        SerializadorEventos.DesdeOutbox(registro).Should().Be(Evento);
    }
}
