using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Domain.Comun;
using LogiMarket.Infrastructure.Pagos;

namespace LogiMarket.Infrastructure.Notificaciones;

/// <summary>Producto concreto: aviso por correo simulado (valida destinatario y arma un mensaje con asunto).</summary>
public sealed class NotificadorEmailSimulado(IModoDemostracion demo, IReloj reloj) : INotificador
{
    public string Canal => "EMAIL";

    public Notificacion Enviar(MensajeAviso aviso)
    {
        if (demo.FallarNotificaciones)
        {
            throw new InvalidOperationException("Servidor de correo simulado no disponible (falla inducida).");
        }

        if (!aviso.Destinatario.Contains('@'))
        {
            throw new InvalidOperationException($"Destinatario de correo inválido: {aviso.Destinatario}.");
        }

        var cuerpo = $"Estimado cliente:\n{aviso.Cuerpo}\n\nLogiMarket 360 — mensaje simulado.";
        return new Notificacion(Canal, aviso.Destinatario, aviso.Asunto, cuerpo, aviso.Referencia, reloj.AhoraUtc);
    }
}

/// <summary>Producto concreto: aviso por SMS simulado (máximo 160 caracteres, sin asunto).</summary>
public sealed class NotificadorSmsSimulado(IModoDemostracion demo, IReloj reloj) : INotificador
{
    public const int LongitudMaxima = 160;

    public string Canal => "SMS";

    public Notificacion Enviar(MensajeAviso aviso)
    {
        if (demo.FallarNotificaciones)
        {
            throw new InvalidOperationException("Operador SMS simulado no disponible (falla inducida).");
        }

        var texto = $"LogiMarket: {aviso.Asunto}. {aviso.Cuerpo}";
        if (texto.Length > LongitudMaxima)
        {
            texto = texto[..(LongitudMaxima - 1)] + "…";
        }

        return new Notificacion(Canal, aviso.Destinatario, string.Empty, texto, aviso.Referencia, reloj.AhoraUtc);
    }
}

/// <summary>Creador concreto EMAIL: su método fábrica devuelve un <see cref="NotificadorEmailSimulado"/>.</summary>
public sealed class CreadorNotificacionEmail(IModoDemostracion demo, IReloj reloj) : CreadorNotificador
{
    public override string Canal => "EMAIL";

    public override INotificador CrearNotificador() => new NotificadorEmailSimulado(demo, reloj);
}

/// <summary>Creador concreto SMS: su método fábrica devuelve un <see cref="NotificadorSmsSimulado"/>.</summary>
public sealed class CreadorNotificacionSms(IModoDemostracion demo, IReloj reloj) : CreadorNotificador
{
    public override string Canal => "SMS";

    public override INotificador CrearNotificador() => new NotificadorSmsSimulado(demo, reloj);
}
