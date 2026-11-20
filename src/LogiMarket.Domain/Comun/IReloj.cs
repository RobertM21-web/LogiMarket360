namespace LogiMarket.Domain.Comun;

/// <summary>Fuente de tiempo inyectable para que las políticas con plazos sean deterministas.</summary>
public interface IReloj
{
    DateTime AhoraUtc { get; }
}
