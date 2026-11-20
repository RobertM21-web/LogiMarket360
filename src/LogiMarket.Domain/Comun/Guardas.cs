namespace LogiMarket.Domain.Comun;

internal static class Guardas
{
    public static string Texto(string? valor, string campo, int max = 200)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ReglaNegocioException("CAMPO_REQUERIDO", $"El campo {campo} es obligatorio.");
        }

        var limpio = valor.Trim();
        if (limpio.Length > max)
        {
            throw new ReglaNegocioException("CAMPO_LARGO", $"El campo {campo} admite como máximo {max} caracteres.");
        }

        return limpio;
    }

    public static int Positivo(int valor, string campo)
    {
        if (valor <= 0)
        {
            throw new ReglaNegocioException("CANTIDAD_INVALIDA", $"{campo} debe ser mayor que cero.");
        }

        return valor;
    }
}
