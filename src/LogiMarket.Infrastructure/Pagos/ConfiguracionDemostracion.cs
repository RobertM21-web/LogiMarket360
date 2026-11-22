using LogiMarket.Application.Puertos;

namespace LogiMarket.Infrastructure.Pagos;

/// <summary>Estado de los interruptores del modo demostración (Singleton del proceso).</summary>
public sealed class ConfiguracionDemostracion : IModoDemostracion
{
    private volatile bool _rechazarCobros;
    private volatile bool _fallarNotificaciones;

    public bool RechazarCobros
    {
        get => _rechazarCobros;
        set => _rechazarCobros = value;
    }

    public bool FallarNotificaciones
    {
        get => _fallarNotificaciones;
        set => _fallarNotificaciones = value;
    }
}
