# Requisitos — LogiMarket 360

## 1. Contexto
Empresa que comercializa accesorios de computación individuales y kits anidados. Gestiona catálogo con existencias,
pedidos por cliente, promociones, costo de despacho, cobros simulados, ciclo hasta la entrega, devoluciones totales,
auditoría y consultas operativas. Aplicación de escritorio local (Windows Forms) con base SQLite.

## 2. Actores
| Actor | Descripción | Operaciones |
|---|---|---|
| ADMIN | Administrador | Todas |
| VENTAS | Vendedor | Clientes, borradores, validación, cobro, cancelación, devolución |
| BODEGA | Operador logístico | Despacho, entrega |
| CONSULTA | Auditor/lector | Solo lectura |
| Pasarela de pago simulada | Sistema externo simulado (Legacy / V2) | Autorizar cobro y reembolso |
| Servicio de avisos simulado | EMAIL / SMS | Recibir notificaciones |

## 3. Casos de uso
| ID | Caso de uso | Actor principal |
|---|---|---|
| CU01 | Iniciar / cerrar sesión | Todos |
| CU02 | Gestionar productos (CRUD, búsqueda) | ADMIN |
| CU03 | Gestionar kits y composición | ADMIN |
| CU04 | Gestionar clientes | ADMIN, VENTAS |
| CU05 | Crear/editar borrador de pedido (deshacer/rehacer, plantilla) | ADMIN, VENTAS |
| CU06 | Validar pedido | ADMIN, VENTAS |
| CU07 | Confirmar pedido con cobro simulado | ADMIN, VENTAS |
| CU08 | Cancelar pedido | ADMIN, VENTAS |
| CU09 | Despachar pedido (guía simulada) | ADMIN, BODEGA |
| CU10 | Registrar entrega | ADMIN, BODEGA |
| CU11 | Procesar devolución total | ADMIN, VENTAS |
| CU12 | Ajustar inventario | ADMIN |
| CU13 | Consultar pedidos (filtro), historial, inventario, bitácora, reportes | Todos |
| CU14 | Emitir comprobantes (vista previa / TXT) | Todos (lectura) |
| CU15 | Procesar eventos Outbox pendientes | Sistema / ADMIN |

## 4. Requisitos funcionales
| ID | Requisito | CU | Verificación |
|---|---|---|---|
| RF01 | Autenticar usuarios locales con contraseña hasheada (PBKDF2 + sal) y rol. | CU01 | P01, prueba de hasher |
| RF02 | Restringir cada operación por rol en la capa de aplicación (no en la UI). | todos | P22, P09, pruebas de autorizador |
| RF03 | CRUD de productos con SKU único, precio > 0, peso ≥ 0, stock entero ≥ 0 y búsqueda. | CU02 | pruebas de producto |
| RF04 | Definir kits con productos u otros kits, rechazando referencias circulares. | CU03 | P18 |
| RF05 | Gestionar clientes con categoría NORMAL/VIP y estado activo. | CU04 | pruebas de cliente |
| RF06 | Crear y editar borradores con deshacer/rehacer (5 estados) y plantillas clonables. | CU05 | P14, P29 |
| RF07 | Calcular subtotal, promoción excluyente (MAYORISTA > VIP > NORMAL) y tarifa de entrega. | CU05–CU07 | P02–P04, P32 |
| RF08 | Validar pedidos mediante cadena: cliente → líneas → stock → condiciones de confirmación. | CU06 | P05, P23 |
| RF09 | Confirmar con cobro simulado único, idempotente, descontando stock una sola vez. | CU07 | P02, P06, P07, P36 |
| RF10 | Cancelar solo desde BORRADOR o VALIDADO. | CU08 | P08, P31 |
| RF11 | Despachar solo pedidos PAGADOS generando guía simulada; registrar entrega. | CU09–CU10 | P09, P12 |
| RF12 | Procesar devolución total única: DEFECTO ≤ 30 días, ARREPENTIMIENTO ≤ 7 días sin uso; reembolso sin flete. | CU11 | P33, P35 |
| RF13 | Registrar movimientos de inventario (+/−) con motivo y referencia; ajustes auditados. | CU12 | P02, P35 |
| RF14 | Filtrar pedidos con lenguaje `ESTADO`, `TOTAL`, `CLIENTE`, `AND`, `OR`, paréntesis. | CU13 | P25, P26 |
| RF15 | Emitir comprobantes de venta y devolución en vista previa y archivo TXT. | CU14 | P17 |
| RF16 | Guardar notificaciones/auditorías como eventos Outbox en la misma transacción y procesarlas después. | CU15 | P30, P37 |
| RF17 | Mostrar indicadores de sesión, panel general, reportes por rango de fechas y bitácora. | CU13 | P15, P28 |

## 5. Requisitos no funcionales (medibles)
| ID | Atributo | Requisito | Métrica / verificación |
|---|---|---|---|
| RNF01 | Integridad | Cobro, stock, pedido y Outbox se confirman en una única transacción. | P06/P20: 0 registros parciales tras fallo inducido |
| RNF02 | Integridad | Sin sobreventa bajo concurrencia. | P36: 2 cobros concurrentes con stock 1 → exactamente 1 aprobado, stock ≥ 0 |
| RNF03 | Mantenibilidad | Domain sin referencias; Application sin EF/SQLite/WinForms. | Prueba de arquitectura: 0 referencias prohibidas |
| RNF04 | Mantenibilidad | Añadir una promoción no modifica la calculadora. | P32: nueva política registrada sin editar `CalculadoraPedido` |
| RNF05 | Usabilidad | Navegación completa por teclado y redimensionado sin solapamientos. | Verificación manual en 1024×640 y 1920×1080; TabIndex en todos los formularios |
| RNF06 | Rendimiento | Confirmación de pedido < 500 ms y carga de 1.000 filas de catálogo visual < 1 s en equipo de referencia. | Cronómetro en pruebas de integración / P21 |
| RNF07 | Seguridad local | Contraseñas hasheadas con PBKDF2-SHA256 (≥ 100.000 iteraciones, sal de 16 bytes). | Inspección de tabla `Usuarios`: 0 contraseñas en claro |
| RNF08 | Trazabilidad | Toda operación de cobro, despacho, devolución, ajuste y denegación genera registro de auditoría con fecha, usuario y correlación. | P19, P22: entradas de auditoría verificadas |
| RNF09 | Testabilidad | ≥ 25 pruebas automatizadas ejecutables con `dotnet test` sin interfaz gráfica. | Resumen de `dotnet test` |
| RNF10 | Recuperabilidad | Eventos Outbox pendientes se procesan exactamente una vez tras reiniciar el procesador. | P37 |
| RNF11 | Persistencia | Datos persisten tras cerrar/reabrir la aplicación. | P10 |
