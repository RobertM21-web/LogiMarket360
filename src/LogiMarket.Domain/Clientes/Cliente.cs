using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Clientes;

public enum CategoriaCliente
{
    Normal = 0,
    Vip = 1,
}

/// <summary>Canal preferido para avisos: determina el creador del Factory Method de notificaciones.</summary>
public enum CanalNotificacion
{
    Email = 0,
    Sms = 1,
}

/// <summary>Cliente comercial. Raíz de agregado.</summary>
public class Cliente
{
    private Cliente()
    {
    }

    public Cliente(string codigo, string nombre, string identificacion, CategoriaCliente categoria, string email, string telefono, CanalNotificacion canal)
    {
        Codigo = Guardas.Texto(codigo, "Código", 20).ToUpperInvariant();
        Actualizar(nombre, identificacion, categoria, email, telefono, canal);
        Activo = true;
    }

    public int Id { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string Identificacion { get; private set; } = string.Empty;

    public CategoriaCliente Categoria { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string Telefono { get; private set; } = string.Empty;

    public CanalNotificacion CanalPreferido { get; private set; }

    public bool Activo { get; private set; }

    public void Actualizar(string nombre, string identificacion, CategoriaCliente categoria, string email, string telefono, CanalNotificacion canal)
    {
        Nombre = Guardas.Texto(nombre, "Nombre", 120);
        Identificacion = Guardas.Texto(identificacion, "Identificación", 20);
        Categoria = categoria;
        Email = email?.Trim() ?? string.Empty;
        Telefono = telefono?.Trim() ?? string.Empty;
        if (Email.Length > 0 && !Email.Contains('@'))
        {
            throw new ReglaNegocioException("EMAIL_INVALIDO", "El correo electrónico no tiene un formato válido.");
        }

        if (canal == CanalNotificacion.Email && Email.Length == 0)
        {
            throw new ReglaNegocioException("CONTACTO_REQUERIDO", "Para avisos por EMAIL el cliente debe tener correo.");
        }

        if (canal == CanalNotificacion.Sms && Telefono.Length == 0)
        {
            throw new ReglaNegocioException("CONTACTO_REQUERIDO", "Para avisos por SMS el cliente debe tener teléfono.");
        }

        CanalPreferido = canal;
    }

    public void Desactivar() => Activo = false;

    public void Activar() => Activo = true;

    public string Destino => CanalPreferido == CanalNotificacion.Email ? Email : Telefono;
}
