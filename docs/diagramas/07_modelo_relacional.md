# Modelo relacional (SQLite, migración `20261122150500_Inicial`)

```mermaid
erDiagram
    Cliente ||--o{ Pedido : realiza
    Pedido ||--|{ PedidoDetalle : contiene
    PedidoDetalle ||--|{ PedidoDetalleComponente : instantanea
    Producto ||--o{ PedidoDetalleComponente : referencia
    Pedido ||--o{ HistorialEstadoPedido : historial
    Pedido ||--o| Pago : unico
    Pedido ||--o| Despacho : unico
    Pedido ||--o| Devolucion : unica
    Kit ||--|{ KitComponente : compone
    Producto ||--o{ KitComponente : hoja
    Producto ||--o{ MovimientoInventario : traza

    Producto {
        int Id PK
        string Sku UK
        string Nombre
        string Categoria
        decimal Precio "CHECK mayor que 0"
        decimal PesoKg
        int Stock "CHECK no negativo"
        int Version "token de concurrencia"
    }
    Kit {
        int Id PK
        string Codigo UK
        string Nombre
        bool Activo
    }
    KitComponente {
        int Id PK
        int KitId FK
        int ProductoId FK "exclusivo con KitHijoId"
        int KitHijoId FK
        int Cantidad "CHECK mayor que 0"
    }
    Cliente {
        int Id PK
        string Codigo UK
        string Identificacion UK
        string Categoria
        string CanalPreferido
        bool Activo
    }
    Usuario {
        int Id PK
        string NombreUsuario UK
        string Rol
        string HashContrasena "PBKDF2"
        string Sal
    }
    Pedido {
        int Id PK
        string Codigo UK
        int ClienteId FK
        string Estado
        string TipoEntrega
        decimal Subtotal
        decimal Descuento
        decimal TarifaEntrega
        decimal Total
        string ClaveIdempotencia UK
    }
    PedidoDetalle {
        int Id PK
        int PedidoId FK
        string Codigo
        bool EsKit
        int Cantidad
        decimal PrecioUnitario
    }
    PedidoDetalleComponente {
        int Id PK
        int PedidoDetalleId FK
        int ProductoId FK
        string Sku
        int CantidadPorUnidad
    }
    HistorialEstadoPedido {
        int Id PK
        int PedidoId FK
        string EstadoAnterior
        string EstadoNuevo
        string Usuario
    }
    Pago {
        int Id PK
        int PedidoId FK "UK"
        decimal Monto
        string Proveedor
        string Referencia UK
        string ClaveIdempotencia UK
    }
    Despacho {
        int Id PK
        int PedidoId FK "UK"
        string NumeroGuia UK
        int PlazoDias
    }
    Devolucion {
        int Id PK
        string Codigo UK
        int PedidoId FK "UK"
        string Motivo
        decimal MontoReembolso
        string ReferenciaReembolso UK
    }
    MovimientoInventario {
        long Id PK
        int ProductoId FK
        string Sku
        int Cantidad "CHECK distinto de 0"
        string Referencia
        int StockResultante
    }
    EventoOutbox {
        long Id PK
        string Tipo
        string Carga
        string Estado
        int Intentos
        string ObservadoresCompletados
    }
    Auditoria {
        long Id PK
        string Categoria
        string Evento
        string Resultado
        string Correlacion
    }
    Notificacion {
        long Id PK
        string Canal
        string Destinatario
        string Referencia
    }
```

- `KitComponente` también referencia a `Kit` mediante `KitHijoId` (kits anidados). Un CHECK impide que un kit se contenga a sí mismo; los ciclos indirectos se rechazan en `ArbolCatalogo.RedefinirKit` antes de persistir.
- `Auditoria` se escribe con una conexión independiente (`AuditoriaIndependiente`): un rechazo o un *rollback* de negocio no borra su rastro.
- `EventoOutbox` se inserta en la misma transacción que el cambio de negocio.
- Los índices únicos en `Pago.PedidoId`, `Pago.ClaveIdempotencia`, `Despacho.PedidoId` y `Devolucion.PedidoId` impiden duplicados aunque falle una validación de aplicación.
