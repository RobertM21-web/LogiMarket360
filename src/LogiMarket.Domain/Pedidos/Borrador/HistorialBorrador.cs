using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Pedidos.Borrador;

/// <summary>
/// Cuidador del Memento: conserva hasta <see cref="Capacidad"/> estados completos para deshacer y
/// rehacer. Al confirmar el pedido se limpia y bloquea; nunca revierte cobros ni stock.
/// </summary>
public sealed class HistorialBorrador
{
    public const int Capacidad = 5;

    private readonly LinkedList<BorradorMemento> _deshacer = new();
    private readonly Stack<BorradorMemento> _rehacer = new();

    public bool Bloqueado { get; private set; }

    public bool PuedeDeshacer => !Bloqueado && _deshacer.Count > 0;

    public bool PuedeRehacer => !Bloqueado && _rehacer.Count > 0;

    public int EstadosGuardados => _deshacer.Count;

    /// <summary>Captura el estado actual, ejecuta la edición y descarta el historial de rehacer.</summary>
    public void Ejecutar(PedidoBorrador borrador, string descripcion, Action<PedidoBorrador> edicion)
    {
        VerificarActivo();
        var antes = borrador.CrearMemento(descripcion);
        edicion(borrador);
        _deshacer.AddLast(antes);
        if (_deshacer.Count > Capacidad)
        {
            _deshacer.RemoveFirst();
        }

        _rehacer.Clear();
    }

    public void Deshacer(PedidoBorrador borrador)
    {
        if (!PuedeDeshacer)
        {
            throw new ReglaNegocioException("SIN_DESHACER", "No hay cambios para deshacer.");
        }

        var memento = _deshacer.Last!.Value;
        _deshacer.RemoveLast();
        _rehacer.Push(borrador.CrearMemento(memento.Descripcion));
        borrador.Restaurar(memento);
    }

    public void Rehacer(PedidoBorrador borrador)
    {
        if (!PuedeRehacer)
        {
            throw new ReglaNegocioException("SIN_REHACER", "No hay cambios para rehacer.");
        }

        var memento = _rehacer.Pop();
        _deshacer.AddLast(borrador.CrearMemento(memento.Descripcion));
        borrador.Restaurar(memento);
    }

    /// <summary>Al confirmar: limpia los estados guardados e impide restaurar.</summary>
    public void LimpiarYBloquear()
    {
        _deshacer.Clear();
        _rehacer.Clear();
        Bloqueado = true;
    }

    private void VerificarActivo()
    {
        if (Bloqueado)
        {
            throw new ReglaNegocioException("BORRADOR_BLOQUEADO", "El historial del borrador está cerrado.");
        }
    }
}
