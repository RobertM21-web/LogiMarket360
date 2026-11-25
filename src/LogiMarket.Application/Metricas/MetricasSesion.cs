namespace LogiMarket.Application.Metricas;

/// <summary>Instantánea inmutable de los indicadores de la sesión.</summary>
public sealed record InstantaneaMetricas(
    int PedidosConfirmados,
    decimal VentasUsd,
    int Despachos,
    int Devoluciones,
    int CobrosDenegados,
    int Notificaciones,
    int ComandosEjecutados,
    DateTime InicioSesionUtc);

/// <summary>
/// Singleton (registrado como único en el contenedor DI): indicadores de la sesión compartidos por varias
/// pantallas. Thread-safe mediante bloqueo; la base de datos sigue siendo la fuente de verdad.
/// </summary>
public sealed class MetricasSesion
{
    private readonly object _candado = new();
    private int _pedidos;
    private decimal _ventas;
    private int _despachos;
    private int _devoluciones;
    private int _cobrosDenegados;
    private int _notificaciones;
    private int _comandos;
    private DateTime _inicio = DateTime.UtcNow;

    /// <summary>Se dispara tras cada cambio para que las vistas suscritas se refresquen.</summary>
    public event EventHandler<InstantaneaMetricas>? Cambiaron;

    public InstantaneaMetricas Instantanea
    {
        get
        {
            lock (_candado)
            {
                return new InstantaneaMetricas(_pedidos, _ventas, _despachos, _devoluciones, _cobrosDenegados, _notificaciones, _comandos, _inicio);
            }
        }
    }

    public void RegistrarPedidoConfirmado(decimal total) => Actualizar(() =>
    {
        _pedidos++;
        _ventas += total;
    });

    public void RegistrarDespacho() => Actualizar(() => _despachos++);

    public void RegistrarDevolucion(decimal reembolso) => Actualizar(() =>
    {
        _devoluciones++;
        _ventas -= reembolso;
    });

    public void RegistrarCobroDenegado() => Actualizar(() => _cobrosDenegados++);

    public void RegistrarNotificacion() => Actualizar(() => _notificaciones++);

    public void RegistrarComando() => Actualizar(() => _comandos++);

    public void Reiniciar() => Actualizar(() =>
    {
        _pedidos = _despachos = _devoluciones = _cobrosDenegados = _notificaciones = _comandos = 0;
        _ventas = 0m;
        _inicio = DateTime.UtcNow;
    });

    private void Actualizar(Action cambio)
    {
        InstantaneaMetricas foto;
        lock (_candado)
        {
            cambio();
            foto = new InstantaneaMetricas(_pedidos, _ventas, _despachos, _devoluciones, _cobrosDenegados, _notificaciones, _comandos, _inicio);
        }

        Cambiaron?.Invoke(this, foto);
    }
}
