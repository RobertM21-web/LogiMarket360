# Registro de decisiones

| ID | Decisión | Motivo |
|---|---|---|
| D01 | Framework de pruebas: **xUnit** + FluentAssertions. | Estándar en .NET 8, ejecución paralela por clase, buena integración con `dotnet test`. |
| D02 | Cancelar pedido y procesar devolución: roles **ADMIN y VENTAS**. | La guía no lo fija; son operaciones comerciales vinculadas al cobro. |
| D03 | Ajuste manual de inventario: solo **ADMIN**. | Operación sensible que altera existencias fuera del flujo de venta. |
| D04 | Consultas (pedidos, inventario, bitácora, reportes, comprobantes): **todos los roles**. | Son de lectura. |
| D05 | Gestión de productos y kits: **ADMIN**. Gestión de clientes: **ADMIN y VENTAS**. | Separación entre catálogo y operación comercial. |
| D06 | Wireframe 03 muestra "P03 Cable HDMI" y stocks 30/20/15: se ignora; P03 = **Memoria USB** y stocks oficiales. | La guía declara ilustrativos los datos de las imágenes. |
| D07 | "Sin uso" en ARREPENTIMIENTO se declara mediante una casilla al registrar la devolución (inspección del operador). | No hay sensor físico; la condición es declarativa e inspeccionada en el paso del Template Method. |
| D08 | DEFECTO reintegra stock **solo si** el operador indica que el producto es reutilizable; por defecto **sí reintegra** para cumplir P35 ("un solo reintegro"). ARREPENTIMIENTO aceptado siempre reintegra. | La guía pide "reintegro de stock cuando proceda". |
| D09 | El reloj es un puerto (`IReloj`) para controlar días transcurridos en pruebas (P33). | Determinismo. |
| D10 | Zona horaria: hora local del equipo; las fechas se guardan en UTC y se muestran en local. | Evita ambigüedad en SQLite. |
| D11 | Concurrencia optimista con columna `Version` (entero) en `Producto`, incrementada en cada cambio de stock y marcada como token de concurrencia. | SQLite no tiene `rowversion`. |
| D12 | La clave de idempotencia la genera el borrador al validarse (GUID) y se envía en cada intento de confirmación; UNIQUE en `Pagos.ClaveIdempotencia`. | Repetir la confirmación devuelve el pago original. |
| D13 | Código de pedido `PED-NNN`, devolución `DEV-NNN`, guía `GUIA-2026-NNNN`, referencia de cobro `LEG-…`/`V2-…`. | Coincide con los wireframes. |
| D14 | La categoría del kit es "KIT"; el kit no tiene stock propio: su disponibilidad se deriva de los SKU finales. | Composite + inventario por SKU final. |
| D15 | Los kits se persisten en tabla `Kits` y `KitComponentes` (componente = producto **o** kit, columnas FK excluyentes con CHECK). | Modelo relacional explícito de la guía. |
| D16 | El procesador Outbox se ejecuta tras cada confirmación (en proceso) y bajo demanda desde Auditoría; no hay servicio en segundo plano permanente. | KISS en aplicación de escritorio. |
| D17 | El filtro Interpreter se evalúa en memoria sobre los DTO de pedidos ya cargados. | Garantiza ausencia de SQL concatenado. |
| D18 | Precedencia del Interpreter: paréntesis > comparación > `AND` > `OR`; asociatividad izquierda. | Convención habitual. |
| D19 | Notificaciones simuladas se registran en la tabla `Notificaciones` (canal, destinatario, mensaje). | Comportamiento verificable sin envíos reales. |
| D20 | Los comprobantes TXT se escriben en `%LOCALAPPDATA%\LogiMarket360\comprobantes`. La BD en `%LOCALAPPDATA%\LogiMarket360\logimarket.db`. | No ensuciar la carpeta del programa ni el repositorio. |
