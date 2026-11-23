using LogiMarket.Application.Registros;

namespace LogiMarket.Application.Puertos;

/// <summary>Resultado contractual de un cobro o reembolso simulado.</summary>
public sealed record ResultadoCobro(bool Aprobado, string Proveedor, string? Referencia, string Mensaje)
{
    public static ResultadoCobro Rechazado(string proveedor, string mensaje) => new(false, proveedor, null, mensaje);
}

/// <summary>
/// Puerto de pago estable para los casos de uso. Precondiciones: monto &gt; 0 y clave no vacía
/// (ArgumentException en caso contrario). Postcondición: misma clave ⇒ mismo resultado (idempotente).
/// </summary>
public interface IPasarelaPago
{
    Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default);
}

/// <summary>Compensación de un cobro aprobado cuya confirmación local no pudo persistirse.</summary>
public interface IAnulacionCobro
{
    Task AnularAsync(string referencia, string motivo, CancellationToken ct = default);
}

/// <summary>Reembolso simulado (separado del cobro por ISP).</summary>
public interface IServicioReembolso
{
    Task<ResultadoCobro> ReembolsarAsync(decimal monto, string claveReembolso, CancellationToken ct = default);
}

/// <summary>Proveedor de pago simulado activo (Legacy / V2), elegido por el usuario o el modo demostración.</summary>
public interface ISeleccionProveedorPago
{
    string ProveedorActivo { get; }

    IReadOnlyList<string> Disponibles { get; }

    void Seleccionar(string proveedor);
}

/// <summary>
/// Interruptores del modo demostración. Activan dobles de prueba reales (rechazo de proveedor, caída del
/// servicio de avisos) sin cambiar reglas de negocio ni falsear resultados.
/// </summary>
public interface IModoDemostracion
{
    bool RechazarCobros { get; set; }

    bool FallarNotificaciones { get; set; }
}

/// <summary>Restablece la base local a los datos de prueba (elimina y recrea con la semilla).</summary>
public interface IRestablecimientoDatos
{
    Task<int> RestablecerAsync(CancellationToken ct = default);
}

/// <summary>Contenido de un aviso a cliente.</summary>
public sealed record MensajeAviso(string Destinatario, string Asunto, string Cuerpo, string Referencia);

/// <summary>Producto del Factory Method: notificador con contrato común.</summary>
public interface INotificador
{
    string Canal { get; }

    Notificacion Enviar(MensajeAviso aviso);
}

/// <summary>
/// Creador del Factory Method. <see cref="CrearNotificador"/> es el método fábrica que cada creador
/// concreto (EMAIL, SMS) implementa; <see cref="Avisar"/> es la operación que lo usa.
/// </summary>
public abstract class CreadorNotificador
{
    public abstract string Canal { get; }

    public Notificacion Avisar(MensajeAviso aviso)
    {
        var notificador = CrearNotificador();
        return notificador.Enviar(aviso);
    }

    public abstract INotificador CrearNotificador();
}

/// <summary>Implementador del Bridge: medio de salida de documentos.</summary>
public interface ISalidaDocumento
{
    string Nombre { get; }

    /// <summary>Emite el documento y devuelve una descripción del destino (p. ej. ruta del archivo).</summary>
    string Emitir(string titulo, IReadOnlyList<string> lineas);
}
