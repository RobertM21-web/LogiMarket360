# Diagramas de estados

## Pedido (patrón State)

```mermaid
stateDiagram-v2
    [*] --> BORRADOR : PedidoBuilder.Construir
    BORRADOR --> VALIDADO : Validar (cadena OK)
    BORRADOR --> CANCELADO : Cancelar
    VALIDADO --> PAGADO : Facade, cobro aprobado
    VALIDADO --> CANCELADO : Cancelar
    PAGADO --> DESPACHADO : Despachar con guía
    DESPACHADO --> ENTREGADO : Registrar entrega
    CANCELADO --> [*]
    ENTREGADO --> [*]
```

- Un cobro rechazado deja el pedido en VALIDADO, sin cambios.
- Una devolución aceptada se registra una sola vez sobre un pedido ENTREGADO (`Pedido.Devolucion`).
- Transiciones rechazadas con `TRANSICION_INVALIDA`: PAGADO → CANCELADO, repetir el estado actual, saltos (BORRADOR → PAGADO, VALIDADO → DESPACHADO) y edición fuera de BORRADOR. Pruebas: P31 y P08.

## Evento Outbox

```mermaid
stateDiagram-v2
    [*] --> PENDIENTE : misma transacción del cambio de negocio
    PENDIENTE --> PROCESADO : todos los observadores completados
    PENDIENTE --> PENDIENTE : falla un observador (Intentos + 1, UltimoError)
    PROCESADO --> [*]
```

`ObservadoresCompletados` guarda qué observadores ya atendieron el evento: un reintento solo invoca a los pendientes (P30, P37).
