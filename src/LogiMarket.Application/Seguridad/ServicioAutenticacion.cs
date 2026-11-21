using LogiMarket.Application.Puertos;

namespace LogiMarket.Application.Seguridad;

/// <summary>Cálculo y verificación de hash de contraseñas (PBKDF2 en Infrastructure).</summary>
public interface IHasherContrasenas
{
    (string Hash, string Sal) Hashear(string contrasena);

    bool Verificar(string contrasena, string hash, string sal);
}

public sealed record ResultadoInicioSesion(bool Exito, string Mensaje, UsuarioSesion? Usuario);

/// <summary>Inicio y cierre de sesión local.</summary>
public sealed class ServicioAutenticacion
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IHasherContrasenas _hasher;
    private readonly ISesionActual _sesion;
    private readonly IRegistroAuditoria _auditoria;

    public ServicioAutenticacion(IRepositorioUsuarios usuarios, IHasherContrasenas hasher, ISesionActual sesion, IRegistroAuditoria auditoria)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _sesion = sesion;
        _auditoria = auditoria;
    }

    public async Task<ResultadoInicioSesion> IniciarAsync(string nombreUsuario, string contrasena, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrEmpty(contrasena))
        {
            return new ResultadoInicioSesion(false, "Ingrese usuario y contraseña.", null);
        }

        var usuario = await _usuarios.BuscarPorNombreAsync(nombreUsuario.Trim().ToLowerInvariant(), ct);
        if (usuario is null || !usuario.Activo || !_hasher.Verificar(contrasena, usuario.HashContrasena, usuario.Sal))
        {
            await _auditoria.RegistrarAsync("SESION", "INICIO FALLIDO", $"Usuario '{nombreUsuario.Trim()}'", nombreUsuario.Trim(), ct: ct);
            return new ResultadoInicioSesion(false, "Usuario o contraseña incorrectos.", null);
        }

        var sesion = new UsuarioSesion(usuario.Id, usuario.NombreUsuario, usuario.NombreCompleto, usuario.Rol);
        _sesion.Iniciar(sesion);
        await _auditoria.RegistrarAsync("SESION", "INICIO", $"Rol {usuario.Rol.ToString().ToUpperInvariant()}", usuario.NombreUsuario, ct: ct);
        return new ResultadoInicioSesion(true, "Bienvenido.", sesion);
    }

    public async Task CerrarAsync(CancellationToken ct = default)
    {
        if (_sesion.Usuario is { } u)
        {
            await _auditoria.RegistrarAsync("SESION", "CIERRE", string.Empty, u.NombreUsuario, ct: ct);
        }

        _sesion.Cerrar();
    }
}
