namespace LogiMarket.Application.Registros;

public enum EstadoOutbox
{
    Pendiente = 0,
    Procesado = 1,
}

/// <summary>
/// Evento durable del patrón Outbox: se inserta en la misma transacción que la operación de negocio
/// y se procesa después. Registra qué observadores ya lo atendieron para no duplicar efectos.
/// </summary>
public class EventoOutbox
{
    private EventoOutbox()
    {
    }

    public EventoOutbox(string tipo, string carga, string referencia, DateTime creadoUtc)
    {
        Tipo = tipo;
        Carga = carga;
        Referencia = referencia;
        CreadoUtc = creadoUtc;
        Estado = EstadoOutbox.Pendiente;
    }

    public long Id { get; private set; }

    public string Tipo { get; private set; } = string.Empty;

    /// <summary>Contenido JSON del evento de dominio.</summary>
    public string Carga { get; private set; } = string.Empty;

    /// <summary>Referencia de negocio (p. ej. PED-021).</summary>
    public string Referencia { get; private set; } = string.Empty;

    public DateTime CreadoUtc { get; private set; }

    public EstadoOutbox Estado { get; private set; }

    public int Intentos { get; private set; }

    public string? UltimoError { get; private set; }

    public DateTime? ProcesadoUtc { get; private set; }

    /// <summary>Nombres de observadores que ya procesaron el evento, separados por ';'.</summary>
    public string ObservadoresCompletados { get; private set; } = string.Empty;

    public bool FueAtendidoPor(string observador) =>
        ObservadoresCompletados.Split(';', StringSplitOptions.RemoveEmptyEntries).Contains(observador);

    public void MarcarAtendidoPor(string observador)
    {
        if (!FueAtendidoPor(observador))
        {
            ObservadoresCompletados = string.IsNullOrEmpty(ObservadoresCompletados) ? observador : $"{ObservadoresCompletados};{observador}";
        }
    }

    public void RegistrarFallo(string error)
    {
        Intentos++;
        UltimoError = error.Length > 500 ? error[..500] : error;
    }

    public void MarcarProcesado(DateTime ahoraUtc)
    {
        if (Estado == EstadoOutbox.Procesado)
        {
            return;
        }

        Intentos++;
        Estado = EstadoOutbox.Procesado;
        ProcesadoUtc = ahoraUtc;
        UltimoError = null;
    }
}

/// <summary>Entrada de bitácora: negocio, seguridad, pagos, sesión o sistema.</summary>
public class RegistroAuditoria
{
    private RegistroAuditoria()
    {
    }

    public RegistroAuditoria(string categoria, string evento, string resultado, string detalle, string usuario, string? correlacion, DateTime fechaUtc)
    {
        Categoria = categoria;
        Evento = evento;
        Resultado = resultado;
        Detalle = detalle.Length > 1000 ? detalle[..1000] : detalle;
        Usuario = usuario;
        Correlacion = correlacion;
        FechaUtc = fechaUtc;
    }

    public long Id { get; private set; }

    public DateTime FechaUtc { get; private set; }

    /// <summary>NEGOCIO, SEGURIDAD, PAGO, OUTBOX, SESION, INVENTARIO, COMANDO.</summary>
    public string Categoria { get; private set; } = string.Empty;

    public string Evento { get; private set; } = string.Empty;

    public string Resultado { get; private set; } = string.Empty;

    public string Detalle { get; private set; } = string.Empty;

    public string Usuario { get; private set; } = string.Empty;

    public string? Correlacion { get; private set; }
}

/// <summary>Aviso simulado enviado a un cliente (EMAIL/SMS) y persistido como evidencia verificable.</summary>
public class Notificacion
{
    private Notificacion()
    {
    }

    public Notificacion(string canal, string destinatario, string asunto, string mensaje, string referencia, DateTime fechaUtc)
    {
        Canal = canal;
        Destinatario = destinatario;
        Asunto = asunto;
        Mensaje = mensaje;
        Referencia = referencia;
        FechaUtc = fechaUtc;
    }

    public long Id { get; private set; }

    public string Canal { get; private set; } = string.Empty;

    public string Destinatario { get; private set; } = string.Empty;

    public string Asunto { get; private set; } = string.Empty;

    public string Mensaje { get; private set; } = string.Empty;

    public string Referencia { get; private set; } = string.Empty;

    public DateTime FechaUtc { get; private set; }
}
