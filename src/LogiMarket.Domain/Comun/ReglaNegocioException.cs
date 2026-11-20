namespace LogiMarket.Domain.Comun;

/// <summary>Violación de una invariante o regla de negocio del dominio.</summary>
public class ReglaNegocioException : Exception
{
    public ReglaNegocioException(string codigo, string mensaje)
        : base(mensaje)
    {
        Codigo = codigo;
    }

    /// <summary>Código estable para pruebas y presentación (p. ej. STOCK_INSUFICIENTE).</summary>
    public string Codigo { get; }
}
