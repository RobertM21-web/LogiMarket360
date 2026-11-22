using System.Security.Cryptography;
using LogiMarket.Application.Seguridad;

namespace LogiMarket.Infrastructure.Seguridad;

/// <summary>PBKDF2-SHA256 con sal aleatoria de 16 bytes y 100.000 iteraciones; comparación en tiempo constante.</summary>
public sealed class HasherPbkdf2 : IHasherContrasenas
{
    private const int Iteraciones = 100_000;
    private const int BytesSal = 16;
    private const int BytesHash = 32;

    public (string Hash, string Sal) Hashear(string contrasena)
    {
        ArgumentException.ThrowIfNullOrEmpty(contrasena);
        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(sal));
    }

    public bool Verificar(string contrasena, string hash, string sal)
    {
        if (string.IsNullOrEmpty(contrasena))
        {
            return false;
        }

        var esperado = Convert.FromBase64String(hash);
        var calculado = Rfc2898DeriveBytes.Pbkdf2(contrasena, Convert.FromBase64String(sal), Iteraciones, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(esperado, calculado);
    }
}
