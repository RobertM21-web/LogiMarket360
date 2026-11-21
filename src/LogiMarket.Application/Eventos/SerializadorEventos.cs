using System.Text.Json;
using LogiMarket.Application.Registros;
using LogiMarket.Domain.Eventos;

namespace LogiMarket.Application.Eventos;

/// <summary>Convierte eventos de dominio en registros Outbox y viceversa.</summary>
public static class SerializadorEventos
{
    private static readonly IReadOnlyDictionary<string, Type> Tipos = new[]
    {
        typeof(PedidoPagado), typeof(DespachoRegistrado), typeof(DevolucionAceptada),
    }.ToDictionary(t => t.Name);

    public static EventoOutbox AOutbox(IEventoDominio evento, string referencia, DateTime ahoraUtc) =>
        new(evento.Tipo, JsonSerializer.Serialize(evento, evento.GetType()), referencia, ahoraUtc);

    public static IEventoDominio DesdeOutbox(EventoOutbox registro) =>
        Tipos.TryGetValue(registro.Tipo, out var tipo)
            ? (IEventoDominio)(JsonSerializer.Deserialize(registro.Carga, tipo)
                ?? throw new InvalidOperationException($"Carga vacía en el evento {registro.Id}."))
            : throw new InvalidOperationException($"Tipo de evento desconocido: {registro.Tipo}.");
}
