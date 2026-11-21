using LogiMarket.Domain.Seguridad;

namespace LogiMarket.Application.Seguridad;

/// <summary>Operaciones protegidas del sistema.</summary>
public enum Operacion
{
    Consultar,
    GestionarCatalogo,
    GestionarClientes,
    GestionarBorradores,
    ValidarPedido,
    CobrarPedido,
    CancelarPedido,
    DespacharPedido,
    EntregarPedido,
    ProcesarDevolucion,
    AjustarInventario,
    ProcesarOutbox,
    ModoDemostracion,
}

/// <summary>Usuario autenticado en la sesión local.</summary>
public sealed record UsuarioSesion(int Id, string NombreUsuario, string NombreCompleto, Rol Rol);

/// <summary>Sesión actual (implementada en la capa de presentación o en pruebas).</summary>
public interface ISesionActual
{
    UsuarioSesion? Usuario { get; }

    void Iniciar(UsuarioSesion usuario);

    void Cerrar();
}

/// <summary>Implementación simple de sesión en memoria del proceso.</summary>
public sealed class SesionLocal : ISesionActual
{
    public UsuarioSesion? Usuario { get; private set; }

    public void Iniciar(UsuarioSesion usuario) => Usuario = usuario;

    public void Cerrar() => Usuario = null;
}

/// <summary>Intento de operación sin permiso.</summary>
public sealed class AccesoDenegadoException : Exception
{
    public AccesoDenegadoException(Operacion operacion, string usuario, Rol? rol)
        : base($"Acceso denegado: el rol {rol?.ToString().ToUpperInvariant() ?? "ANÓNIMO"} no puede {Describir(operacion)}.")
    {
        Operacion = operacion;
        NombreUsuario = usuario;
        Rol = rol;
    }

    public Operacion Operacion { get; }

    public string NombreUsuario { get; }

    public Rol? Rol { get; }

    private static string Describir(Operacion op) => op switch
    {
        Operacion.CobrarPedido => "cobrar pedidos",
        Operacion.DespacharPedido => "despachar pedidos",
        Operacion.EntregarPedido => "registrar entregas",
        Operacion.ProcesarDevolucion => "procesar devoluciones",
        Operacion.AjustarInventario => "ajustar inventario",
        Operacion.GestionarCatalogo => "modificar el catálogo",
        Operacion.GestionarClientes => "modificar clientes",
        Operacion.CancelarPedido => "cancelar pedidos",
        _ => $"ejecutar {op}",
    };
}

/// <summary>Matriz rol → operaciones (§2.1 y decisiones D02–D05).</summary>
public static class PoliticaPermisos
{
    private static readonly IReadOnlyDictionary<Rol, IReadOnlySet<Operacion>> Matriz = new Dictionary<Rol, IReadOnlySet<Operacion>>
    {
        [Rol.Admin] = Enum.GetValues<Operacion>().ToHashSet(),
        [Rol.Ventas] = new HashSet<Operacion>
        {
            Operacion.Consultar, Operacion.GestionarClientes, Operacion.GestionarBorradores, Operacion.ValidarPedido,
            Operacion.CobrarPedido, Operacion.CancelarPedido, Operacion.ProcesarDevolucion,
        },
        [Rol.Bodega] = new HashSet<Operacion> { Operacion.Consultar, Operacion.DespacharPedido, Operacion.EntregarPedido },
        [Rol.Consulta] = new HashSet<Operacion> { Operacion.Consultar },
    };

    public static bool Permite(Rol rol, Operacion operacion) => Matriz[rol].Contains(operacion);
}

/// <summary>Registro de intentos de acceso no autorizados (auditoría de seguridad separada).</summary>
public interface IAuditoriaSeguridad
{
    Task RegistrarDenegacionAsync(string usuario, string rol, Operacion operacion, string detalle, CancellationToken ct = default);
}

/// <summary>Verifica permisos en la capa de aplicación (no depende de la interfaz gráfica).</summary>
public interface IAutorizador
{
    UsuarioSesion UsuarioActual { get; }

    bool Puede(Operacion operacion);

    Task VerificarAsync(Operacion operacion, string detalle = "", CancellationToken ct = default);
}

public sealed class Autorizador : IAutorizador
{
    private readonly ISesionActual _sesion;
    private readonly IAuditoriaSeguridad _auditoria;

    public Autorizador(ISesionActual sesion, IAuditoriaSeguridad auditoria)
    {
        _sesion = sesion;
        _auditoria = auditoria;
    }

    public UsuarioSesion UsuarioActual =>
        _sesion.Usuario ?? throw new AccesoDenegadoException(Operacion.Consultar, "anónimo", null);

    public bool Puede(Operacion operacion) =>
        _sesion.Usuario is { } u && PoliticaPermisos.Permite(u.Rol, operacion);

    public async Task VerificarAsync(Operacion operacion, string detalle = "", CancellationToken ct = default)
    {
        if (Puede(operacion))
        {
            return;
        }

        var usuario = _sesion.Usuario;
        await _auditoria.RegistrarDenegacionAsync(usuario?.NombreUsuario ?? "anónimo", usuario?.Rol.ToString().ToUpperInvariant() ?? "ANÓNIMO", operacion, detalle, ct);
        throw new AccesoDenegadoException(operacion, usuario?.NombreUsuario ?? "anónimo", usuario?.Rol);
    }
}
