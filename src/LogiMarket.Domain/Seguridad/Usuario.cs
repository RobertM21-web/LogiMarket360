using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Seguridad;

public enum Rol
{
    Admin = 0,
    Ventas = 1,
    Bodega = 2,
    Consulta = 3,
}

/// <summary>Cuenta local. Solo almacena el hash y la sal de la contraseña, nunca el texto claro.</summary>
public class Usuario
{
    private Usuario()
    {
    }

    public Usuario(string nombreUsuario, string nombreCompleto, Rol rol, string hashContrasena, string sal)
    {
        NombreUsuario = Guardas.Texto(nombreUsuario, "Usuario", 40).ToLowerInvariant();
        NombreCompleto = Guardas.Texto(nombreCompleto, "Nombre completo", 120);
        Rol = rol;
        CambiarContrasena(hashContrasena, sal);
        Activo = true;
    }

    public int Id { get; private set; }

    public string NombreUsuario { get; private set; } = string.Empty;

    public string NombreCompleto { get; private set; } = string.Empty;

    public Rol Rol { get; private set; }

    public string HashContrasena { get; private set; } = string.Empty;

    public string Sal { get; private set; } = string.Empty;

    public bool Activo { get; private set; }

    public void CambiarContrasena(string hash, string sal)
    {
        HashContrasena = Guardas.Texto(hash, "Hash", 200);
        Sal = Guardas.Texto(sal, "Sal", 100);
    }

    public void Desactivar() => Activo = false;
}
