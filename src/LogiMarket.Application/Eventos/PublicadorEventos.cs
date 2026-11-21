using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Eventos;

namespace LogiMarket.Application.Eventos;

/// <summary>Observer: suscriptor de eventos de dominio publicados tras confirmar la transacción.</summary>
public interface IObservadorEventos
{
    /// <summary>Nombre estable usado para registrar en el Outbox que ya atendió el evento.</summary>
    string Nombre { get; }

    bool Interesa(IEventoDominio evento);

    Task ManejarAsync(IEventoDominio evento, CancellationToken ct);
}

public sealed record ResumenProcesamiento(int Procesados, int Fallidos, IReadOnlyList<string> Detalle);

/// <summary>
/// Sujeto del Observer: notifica a los observadores suscritos. Cada observador se marca como completado
/// en el registro Outbox y se confirma de inmediato, de modo que un reintento nunca repite un efecto ya aplicado.
/// </summary>
public sealed class PublicadorEventos
{
    private readonly List<IObservadorEventos> _observadores = new();

    public PublicadorEventos(IEnumerable<IObservadorEventos> observadores)
    {
        _observadores.AddRange(observadores);
    }

    public IReadOnlyList<IObservadorEventos> Observadores => _observadores.AsReadOnly();

    public void Suscribir(IObservadorEventos observador)
    {
        if (_observadores.All(o => o.Nombre != observador.Nombre))
        {
            _observadores.Add(observador);
        }
    }

    public void Desuscribir(string nombre) => _observadores.RemoveAll(o => o.Nombre == nombre);

    /// <summary>Publica el evento del registro Outbox. Devuelve true si todos los interesados lo atendieron.</summary>
    public async Task<bool> PublicarAsync(EventoOutbox registro, IUnidadDeTrabajo uow, IReloj reloj, CancellationToken ct)
    {
        var evento = SerializadorEventos.DesdeOutbox(registro);
        foreach (var observador in _observadores.Where(o => o.Interesa(evento) && !registro.FueAtendidoPor(o.Nombre)))
        {
            try
            {
                await observador.ManejarAsync(evento, ct);
                registro.MarcarAtendidoPor(observador.Nombre);
                await uow.GuardarCambiosAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                uow.DescartarCambios();
                registro.RegistrarFallo($"{observador.Nombre}: {ex.Message}");
                await uow.GuardarCambiosAsync(ct);
                return false;
            }
        }

        registro.MarcarProcesado(reloj.AhoraUtc);
        await uow.GuardarCambiosAsync(ct);
        return true;
    }
}

/// <summary>Procesa los eventos pendientes del Outbox (tras cada confirmación o bajo demanda).</summary>
public sealed class ProcesadorOutbox
{
    private readonly IRepositorioOutbox _outbox;
    private readonly PublicadorEventos _publicador;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IReloj _reloj;

    public ProcesadorOutbox(IRepositorioOutbox outbox, PublicadorEventos publicador, IUnidadDeTrabajo uow, IReloj reloj)
    {
        _outbox = outbox;
        _publicador = publicador;
        _uow = uow;
        _reloj = reloj;
    }

    public async Task<ResumenProcesamiento> ProcesarPendientesAsync(CancellationToken ct = default)
    {
        var detalle = new List<string>();
        int ok = 0, fallos = 0;
        foreach (var registro in await _outbox.ListarPendientesAsync(100, ct))
        {
            if (await _publicador.PublicarAsync(registro, _uow, _reloj, ct))
            {
                ok++;
                detalle.Add($"{registro.Tipo} {registro.Referencia}: PROCESADO");
            }
            else
            {
                fallos++;
                detalle.Add($"{registro.Tipo} {registro.Referencia}: PENDIENTE ({registro.UltimoError})");
            }
        }

        return new ResumenProcesamiento(ok, fallos, detalle);
    }
}
