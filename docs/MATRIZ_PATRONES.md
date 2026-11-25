# Matriz de patrones GoF — LogiMarket 360 (23/23)

Cada patrón participa en un caso de uso real del flujo venta → cobro → despacho → devolución. Para cada uno se indica el problema
concreto, las clases participantes, la ubicación en el código, el recorrido de ejecución, la prueba automatizada que lo
demuestra, la evidencia observable en la aplicación y el balance beneficio/costo.

Convenciones de rutas: `D/` = `src/LogiMarket.Domain`, `A/` = `src/LogiMarket.Application`, `I/` = `src/LogiMarket.Infrastructure`,
`W/` = `src/LogiMarket.WinForms`, `T/` = `tests/LogiMarket.Tests`. Capturas en `docs/evidencias/capturas/`.

## Tabla de cobertura

| # | Patrón | Tipo | Participantes principales | Archivo / método | Prueba (P) | Evidencia en ejecución | Estado |
|---|---|---|---|---|---|---|---|
| 1 | Factory Method | Creacional | `CreadorNotificador`, `CreadorNotificacionEmail`, `CreadorNotificacionSms`, `NotificacionClienteObserver` | `A/Puertos/PuertosExternos.cs` `CreadorNotificador.Avisar` / `CrearNotificador`; `I/Notificaciones/Notificadores.cs` | P11 | `12_notificaciones_P11.png` (EMAIL para Ana, SMS para Luis) | ✅ |
| 2 | Abstract Factory | Creacional | `IFabricaEntrega`, `FabricaEntregaEstandar`, `FabricaEntregaExpres`, `ITarifaEntrega`, `IPlazoEntrega`, `IConstructorGuia`, `CatalogoFamiliasEntrega` | `D/Entrega/FamiliasEntrega.cs` `CrearTarifa` / `CrearPlazo` / `CrearConstructorGuia` | P12 | `08_despacho_detalle_PED-00x.png` (tarifa, plazo y guía por familia) | ✅ |
| 3 | Builder | Creacional | `PedidoBuilder`, `PedidoBorrador.AplicarA`, `Pedido` | `D/Pedidos/PedidoBuilder.cs` `ParaCliente` → `Agregar` → `ConEntrega` → `Construir` | P13 | `05_validacion_P02_ana.png` | ✅ |
| 4 | Prototype | Creacional | `IPrototipo<T>`, `PedidoBorrador.Clonar`, `IElementoCatalogo.Clonar`, `KitCompuesto.Clonar` | `D/Pedidos/Borrador/PedidoBorrador.cs` `Clonar`; `D/Catalogo/Composite/KitCompuesto.cs` `Clonar` | P14 | Botón «Repetir último pedido (Prototype)» en Carrito | ✅ |
| 5 | Singleton | Creacional | `MetricasSesion` (registrado `AddSingleton`) | `A/Metricas/MetricasSesion.cs`; registro en `I/ContenedorLogiMarket.cs` | P15 + prueba de composition root | Barra de estado `MetricasSesion #hash` idéntico en todas las pantallas | ✅ |
| 6 | Adapter | Estructural | `PasarelaLegadaSimulada.CobrarCentavos(long,string)` (legado, intacto), `PasarelaLegadaAdapter : IProveedorPago`, `PasarelaV2Simulada` | `I/Pagos/Pasarelas.cs` `PasarelaLegadaAdapter.CobrarAsync`; `I/Pagos/Legado/PasarelaLegadaSimulada.cs` | P16 | `12_auditoria_pagos_P19.png` (`CobrarCentavos(5900) → 00|LEG-000001`) | ✅ |
| 7 | Bridge | Estructural | Abstracción `Comprobante` (`ComprobanteVenta`, `ComprobanteDevolucion`) × implementador `ISalidaDocumento` (`SalidaVistaPrevia`, `SalidaArchivoTxt`) | `A/Comprobantes/Comprobantes.cs`; `I/Documentos/SalidasDocumento.cs` | P17 | `11_reportes_comprobante_venta_vista_previa.png`, `11_reportes_comprobante_devolucion_txt.png`, TXT real en `capturas/` | ✅ |
| 8 | Composite | Estructural | `IElementoCatalogo`, `ProductoSimple`, `KitCompuesto`, `ArbolCatalogo` (ciclos) | `D/Catalogo/Composite/*`; `D/Catalogo/ArbolCatalogo.cs` `RedefinirKit` | P18 | `03_catalogo_arbol_kit_teletrabajo.png`, `03_catalogo_ciclo_rechazado.png` | ✅ |
| 9 | Decorator | Estructural | `AuditoriaPagoDecorator : IPasarelaPago` envuelve al `EnrutadorPasarelas` | `I/Pagos/AuditoriaPagoDecorator.cs` `CobrarAsync` | P19 | `12_auditoria_pagos_P19.png` (COBRO INICIO → PASARELA → COBRO FIN con el mismo `COR-…`) | ✅ |
| 10 | Facade | Estructural | `ConfirmacionPedidoFacade` | `A/Pedidos/ConfirmacionPedidoFacade.cs` `ConfirmarAsync` | P20, P06, P07 | `07_cobro_P02_ana.png`, `07_cobro_P07_repeticion_idempotente.png` | ✅ |
| 11 | Flyweight | Estructural | `CatalogoVisualFlyweightFactory`, `MetadatosSku` (intrínseco), `FilaCatalogoVista` / `FilaVistaCatalogo` (extrínseco) | `A/Catalogo/CatalogoVisualFlyweight.cs` `Obtener`; `A/Catalogo/ConsultaCatalogoVisual.cs` `CargarAsync` | P21 | Grilla de Catálogo (`03_catalogo_arbol_kit_teletrabajo.png`); `resultado_pruebas.txt` (10 000 filas, 5 instancias, −88,9 % bytes) | ✅ |
| 12 | Proxy | Estructural | `PagoAutorizadoProxy : IPasarelaPago` | `A/Pagos/PagoAutorizadoProxy.cs` `CobrarAsync` | P22 | `07_cobro_P22_consulta_denegado.png`, `02_panel_rol_consulta.png` | ✅ |
| 13 | Chain of Responsibility | Comportamental | `EslabonValidacion`, `ClienteActivoValidador` → `LineasValidador` → `StockValidador` → `CondicionesConfirmacionValidador`, `CadenaValidacionPedido` | `A/Pedidos/Validacion/CadenaValidacionPedido.cs` | P23 | `05_validacion_P05_stock_insuficiente.png` (eslabón «Reglas de stock») | ✅ |
| 14 | Command | Comportamental | `IComandoNegocio`, `ComandoConEventos`, `ConfirmarPedidoCommand`, `CancelarPedidoCommand`, `DespacharPedidoCommand`, `ProcesarDevolucionCommand`, `InvocadorComandos`, `FabricaComandos` | `A/Comandos/Comandos.cs` `InvocadorComandos.EjecutarAsync` | P24 | `12_comandos_P24.png` (historial de invocaciones) | ✅ |
| 15 | Interpreter | Comportamental | `InterpreteFiltro` (léxico + parser descendente) → árbol `IExpresionFiltro` (`ExpresionY`, `ExpresionO`, `ExpresionEstado`, `ExpresionTotal`, `ExpresionCliente`) | `A/Consultas/Filtro/InterpreteFiltro.cs` `Filtrar` | P25, P26 | `06_pedidos_filtro_P25.png`, `06_pedidos_filtro_invalido_P26.png` | ✅ |
| 16 | Iterator | Comportamental | `IIteradorComponentes`, `IteradorComponentesFisicos` | `D/Catalogo/Recorrido/IteradorComponentesFisicos.cs` `HaySiguiente` / `Siguiente` | P27 | Expansión a SKU usada por stock y movimientos (`10_inventario_movimientos.png`) | ✅ |
| 17 | Mediator | Comportamental | `FormularioPrincipalMediator`, `IColegaPantalla`, `PantallaBase` y 11 pantallas | `W/Mediador/FormularioPrincipalMediator.cs` `NotificarAsync` | P28 | Tras validar, el carrito navega a Cobro sin conocerlo (`05_validacion_*` → `07_cobro_*`) | ✅ |
| 18 | Memento | Comportamental | Originador `PedidoBorrador`, memento `BorradorMemento`, cuidador `HistorialBorrador` (máx. 5) | `D/Pedidos/Borrador/HistorialBorrador.cs` `Ejecutar` / `Deshacer` / `Rehacer` | P29 | `05_carrito_P29_tres_ediciones.png`, `_dos_deshacer.png`, `_un_rehacer.png` | ✅ |
| 19 | Observer | Comportamental | `PublicadorEventos`, `IObservadorEventos` (`NotificacionClienteObserver`, `AuditoriaNegocioObserver`, `MetricasObserver`), `ProcesadorOutbox` | `A/Eventos/PublicadorEventos.cs` `PublicarAsync`, `ProcesarPendientesAsync` | P30, P37 | `12_outbox_P37_pendiente.png` → `12_outbox_P37_procesado.png` | ✅ |
| 20 | State | Comportamental | `EstadoPedido` + `EstadoBorrador`, `EstadoValidado`, `EstadoPagado`, `EstadoDespachado`, `EstadoEntregado`, `EstadoCancelado` | `D/Pedidos/Estados/EstadoPedido.cs`; uso en `D/Pedidos/Pedido.cs` | P31, P08 | `06_pedidos_historial_PED001.png` (línea de tiempo) | ✅ |
| 21 | Strategy | Comportamental | `IPoliticaDescuento` (`PoliticaSinDescuento`, `PoliticaVip10`, `PoliticaMayorista15`), `SelectorPoliticaDescuento`, `CalculadoraPedido` | `D/Promociones/*` `SelectorPoliticaDescuento.Seleccionar` | P32 | `05_carrito_P02_ana.png` (VIP 10 %), `05_carrito_P04_carmen.png` (MAYORISTA 15 %) | ✅ |
| 22 | Template Method | Comportamental | `ProcesarDevolucion` (`ProcesarAsync` fija la secuencia), `DevolucionDefecto`, `DevolucionArrepentimiento` | `A/Devoluciones/ProcesarDevolucion.cs` | P33, P35 | `09_devolucion_P35_ana_54.png`, `09_devolucion_P33_arrepentimiento_usado_rechazado.png` | ✅ |
| 23 | Visitor | Comportamental | `IVisitanteCatalogo`, `DesgloseSkuVisitor`, `PesoLogisticoVisitor`; `Aceptar` en `ProductoSimple`/`KitCompuesto` | `D/Catalogo/Visitantes/*`; `A/Catalogo/ConsultaCatalogoVisual.cs` `CargarAsync` | P34 | Desglose «P01 ×1, P02 ×1, P03 ×1» y peso 1.00 kg en Catálogo (`03_catalogo_arbol_kit_teletrabajo.png`) | ✅ |

**Cobertura: 23/23 patrones con colaboración real, archivo ubicado, prueba automatizada aprobada y evidencia observable.**

---

## Detalle por patrón

### 1. Factory Method — notificaciones EMAIL / SMS
- **Problema:** cada cliente recibe los avisos por su canal preferido; el observador de notificación no debe conocer los notificadores concretos y la interfaz nunca debe elegir el canal.
- **Participantes:** creador abstracto `CreadorNotificador` (Application, método fábrica `CrearNotificador()` y operación `Avisar`), creadores concretos `CreadorNotificacionEmail` y `CreadorNotificacionSms` (Infrastructure) que devuelven `NotificadorEmailSimulado` / `NotificadorSmsSimulado` con el mismo contrato `INotificador`.
- **Recorrido:** `ConfirmarPedidoCommand` → Outbox → `PublicadorEventos` → `NotificacionClienteObserver.ManejarAsync` elige el creador por `Cliente.CanalPreferido` → `Avisar` → registro en `Notificaciones`. Lo mismo ocurre con `DespachoRegistrado` y `DevolucionAceptada`.
- **Prueba:** `P11_factory_method_elige_email_o_sms_segun_el_cliente_y_registra_el_aviso`.
- **Beneficio / costo:** añadir un canal (p. ej. WhatsApp) es registrar un creador nuevo en DI; costo: una jerarquía paralela de creadores.

### 2. Abstract Factory — familias de entrega ESTÁNDAR / EXPRÉS
- **Problema:** tarifa, plazo y guía deben pertenecer a la misma familia; mezclar tarifa exprés con plazo estándar sería un error silencioso.
- **Participantes:** `IFabricaEntrega` con `CrearTarifa`, `CrearPlazo`, `CrearConstructorGuia`; `FabricaEntregaEstandar` (USD 5, 3 días, «ENVÍO ESTÁNDAR») y `FabricaEntregaExpres` (USD 12, 1 día, «ENVÍO EXPRÉS»); productos internos `TarifaFija`, `PlazoEnDias`, `ConstructorGuiaFamilia` que validan la familia.
- **Recorrido:** valoración del pedido (`CalculadoraPedido`) usa la tarifa; `ServicioLogistica.DespacharAsync` obtiene la fábrica de la familia y construye la guía `GUIA-2026-0001`.
- **Prueba:** `P12_cada_familia_produce_tarifa_plazo_y_guia_coherentes` (×2) y `P12_no_se_pueden_mezclar_productos_de_familias_distintas`.
- **Beneficio / costo:** una familia nueva no cambia el caso de uso; costo: añadir un producto a la familia obliga a tocar todas las fábricas.

### 3. Builder — `PedidoBuilder`
- **Problema:** un pedido necesita cliente, líneas (simples y kits), forma de entrega e instantáneas de precio antes de existir; un constructor con muchos parámetros o setters permitiría agregados inválidos.
- **Participantes:** `PedidoBuilder` (`ParaCliente`, `Agregar`, `ConEntrega`, `ConstruirContenido`, `Construir`) y el agregado `Pedido`.
- **Recorrido:** Carrito → «Validar pedido» → `ServicioPedidos.ValidarAsync` → `PedidoBorrador.AplicarA(builder)` → `Construir` (rechaza sin cliente o sin líneas).
- **Prueba:** `P13_pedido_sin_cliente_es_rechazado`, `P13_pedido_sin_lineas_es_rechazado`, `P13_pedido_completo_multirrenglon_crea_agregado_valido_con_instantaneas`.
- **Beneficio / costo:** el agregado nace válido; costo: una clase más por agregado.

### 4. Prototype — plantilla de carrito
- **Problema:** repetir un pedido frecuente sin reconstruirlo línea a línea y sin arrastrar id ni estado de pago.
- **Participantes:** `IPrototipo<T>`, `PedidoBorrador.Clonar()` (copia profunda de líneas y de la estructura anidada del kit mediante `IElementoCatalogo.Clonar`).
- **Recorrido:** Carrito → «Repetir último pedido (Prototype)» → clon editable desbloqueado.
- **Prueba:** `P14_clonar_plantilla_produce_copia_profunda_independiente`, `P14_clon_de_borrador_bloqueado_vuelve_a_ser_editable`.
- **Beneficio / costo:** reutilización sin acoplarse a la construcción; costo: mantener la copia profunda cuando cambian las colecciones.

### 5. Singleton — `MetricasSesion`
- **Problema:** varias pantallas y observadores deben mostrar los mismos contadores de sesión sin que la base de datos deje de ser la fuente de verdad.
- **Participantes:** `MetricasSesion` (contadores con `lock`, instantánea inmutable), registrado una sola vez con `AddSingleton` (sin `static Instance`).
- **Recorrido:** `MetricasObserver`, `PagoAutorizadoProxy`, `ConfirmarPedidoCommand.AlDenegarseAcceso` e invocador escriben; la barra de estado de la ventana lee.
- **Prueba:** `P15_singleton_metricas_es_la_misma_instancia_en_distintos_ambitos_y_es_thread_safe`; `Composition_root_valida_el_grafo_y_construye_la_ventana_con_sus_11_pantallas`.
- **Beneficio / costo:** un único punto de lectura; costo: estado global que hay que reiniciar (lo hace «Restablecer datos de prueba»).

### 6. Adapter — pasarela legada
- **Problema:** el proveedor legado expone `CobrarCentavos(long, string)` y responde con texto `"00|LEG-…"`; la aplicación trabaja con `CobrarAsync(decimal, clave)` y no puede modificarse el código legado.
- **Participantes:** adaptado `PasarelaLegadaSimulada` (sin cambios), adaptador `PasarelaLegadaAdapter : IProveedorPago`, segundo proveedor `PasarelaV2Simulada` con la misma interfaz; `EnrutadorPasarelas` elige según `SeleccionProveedorPago` (modo demostración).
- **Recorrido:** Cobro → «Confirmar cobro» → Facade → Proxy → Decorator → `EnrutadorPasarelas` → `PasarelaLegadaAdapter.CobrarAsync` convierte USD 59.00 en `CobrarCentavos(5900)` y traduce `00|LEG-000001` a un resultado aprobado con referencia `LEG-000001`.
- **Prueba:** `P16_adapter_legado_y_proveedor_v2_cumplen_el_mismo_contrato`.
- **Beneficio / costo:** el proveedor se sustituye por DI sin tocar la Facade (LSP); costo: mantener la traducción de unidades y códigos de respuesta.

### 7. Bridge — comprobantes × salidas
- **Problema:** dos documentos (venta, devolución) y dos salidas (vista previa, TXT) producirían 4 clases duplicadas.
- **Participantes:** abstracción `Comprobante` con refinamientos `ComprobanteVenta` y `ComprobanteDevolucion`; implementador `ISalidaDocumento` con `SalidaVistaPrevia` y `SalidaArchivoTxt`; `ServicioComprobantes.EmitirAsync(pedidoId, tipo, salida)`.
- **Recorrido:** Reportes → seleccionar PED-001 → «Vista previa» o «Exportar TXT» → `ServicioComprobantes.EmitirAsync` crea el comprobante del tipo elegido y lo entrega a la salida elegida.
- **Prueba:** `P17_bridge_cuatro_combinaciones_con_txt_real_y_vista_previa_visible`.
- **Evidencia real:** `Comprobante_de_devolución_DEV-001_*.txt` generado por la aplicación.
- **Beneficio / costo:** n + m clases en vez de n × m; costo: una indirección más entre documento y salida.

### 8. Composite — catálogo de productos y kits
- **Problema:** un kit contiene productos u otros kits; precio, expansión de SKU y peso deben calcularse de forma uniforme y sin ciclos.
- **Participantes:** `IElementoCatalogo`, hoja `ProductoSimple`, compuesto `KitCompuesto`, `ArbolCatalogo.Construir` y `ArbolCatalogo.RedefinirKit` (detección de ciclos antes de persistir).
- **Recorrido:** Catálogo → `ConsultaCatalogoVisual.CargarAsync` construye el árbol y obtiene `Precio` de cada kit (KIT_OFICINA 50.00, KIT_TELETRABAJO 60.00). Al editar una composición, `ServicioCatalogo.GuardarComposicionAsync` → `RedefinirKit` rechaza KIT_OFICINA ← KIT_TELETRABAJO con `KIT_CICLICO`. En la venta, `ExpansionSku` recorre el mismo árbol para descontar stock.
- **Prueba:** `P18_precios_de_kits_se_calculan_recursivamente` (50 / 60), `P18_kit_anidado_se_expande_hasta_los_sku_finales`, `P18_kit_no_puede_contenerse_a_si_mismo`, `P18_ciclo_indirecto_es_rechazado_sin_modificar_la_composicion`, `P34_P21_la_pantalla_de_catalogo_*`.
- **Beneficio / costo:** el cliente trata igual a un producto y a un kit anidado; costo: la detección de ciclos debe ejecutarse antes de cada cambio de composición.

### 9. Decorator — auditoría de cobro
- **Problema:** registrar inicio, resultado o error de cada cobro con un id de correlación sin tocar el proveedor.
- **Participantes:** `AuditoriaPagoDecorator : IPasarelaPago` que envuelve `EnrutadorPasarelas`; `ContextoCorrelacion` (AsyncLocal) propaga el `COR-…` a la pasarela.
- **Recorrido:** `PagoAutorizadoProxy` → `AuditoriaPagoDecorator.CobrarAsync` registra `COBRO INICIO` → `EnrutadorPasarelas` → `PasarelaLegadaAdapter` / `PasarelaV2Simulada` (registra `PASARELA`) → el Decorator registra `COBRO FIN` o el error, siempre con el mismo `COR-…`.
- **Prueba:** `P19_decorator_registra_inicio_pasarela_y_fin_con_la_misma_correlacion` (salida en `resultado_pruebas.txt`).
- **Beneficio / costo:** la auditoría se añade o quita en la composición de DI, sin modificar proveedores; costo: el orden de los envoltorios importa (la denegación del Proxy nunca llega al Decorator, a propósito).

### 10. Facade — `ConfirmacionPedidoFacade`
- **Problema:** confirmar implica autorización, revalidación (Chain), cobro, pago + stock + Outbox en una transacción, idempotencia y compensación; la interfaz debe ver una sola operación.
- **Participantes:** `ConfirmacionPedidoFacade.ConfirmarAsync` coordina `IAutorizador`, `CadenaValidacionPedido`, `IPasarelaPago`, repositorios, `IUnidadDeTrabajo` e `IAnulacionCobro`. No contiene SQL ni reglas de descuento.
- **Recorrido:** Cobro → `ConfirmarPedidoCommand` → `ConfirmarAsync`: autoriza → busca pago previo por clave de idempotencia → revalida con la cadena → cobra → en una transacción guarda el pago, descuenta stock, cambia a PAGADO y agrega el evento Outbox → si algo falla tras cobrar, compensa el cobro.
- **Prueba:** `P20_facade_con_falla_en_un_colaborador_no_deja_persistencia_parcial_y_compensa_el_cobro`, `P06`, `P07`, `P36`.
- **Beneficio / costo:** la interfaz y las pruebas usan una sola llamada con garantías transaccionales; costo: la Facade concentra muchas dependencias (se mantiene en 159 líneas que solo coordinan).

### 11. Flyweight — metadatos de SKU en vistas grandes
- **Problema:** una vista de miles de renglones repetiría nombre, categoría y descripción del mismo SKU.
- **Participantes:** `CatalogoVisualFlyweightFactory.Obtener` (caché Singleton de `MetadatosSku` inmutables, reemplazados si el producto cambia), renglones con estado extrínseco (`FilaCatalogoVista`: precio, stock, estado; `FilaVistaCatalogo`: stock, cantidad, precio de venta).
- **Recorrido:** Catálogo → `ConsultaCatalogoVisual.CargarAsync` → cada renglón pide sus metadatos a la fábrica; al recargar la pantalla o abrir otro ámbito se reutiliza la misma instancia por SKU.
- **Medición documentada:** 10 000 filas; 5 instancias compartidas frente a 10 000; bytes asignados 560 536 frente a 5 040 256 (−88,9 %), medidos con `GC.GetAllocatedBytesForCurrentThread` en el mismo hilo y con los mismos datos.
- **Prueba:** `P21_flyweight_comparte_metadatos_entre_mil_filas_y_reduce_memoria_en_escenario_equivalente` (`ReferenceEquals == true`), `P21_editar_un_producto_reemplaza_su_flyweight_y_la_vista_no_muestra_metadatos_obsoletos`, `P34_P21_la_pantalla_de_catalogo_*`.
- **Beneficio / costo:** memoria proporcional al número de SKU y no al de filas; costo: el estado compartido debe ser inmutable y la caché debe invalidarse al editar un producto.

### 12. Proxy — `PagoAutorizadoProxy`
- **Problema:** solo VENTAS y ADMIN pueden cobrar, también cuando el cobro se invoca sin interfaz.
- **Participantes:** `PagoAutorizadoProxy : IPasarelaPago` verifica con `IAutorizador` antes de delegar; registra la denegación en auditoría de seguridad y en `MetricasSesion`.
- **Recorrido:** el Proxy es el `IPasarelaPago` que DI entrega a cualquier consumidor. La Facade autoriza primero (`CobrarPedido`); si otro caso de uso o una prueba llama directamente a la pasarela con el rol CONSULTA o BODEGA, `PagoAutorizadoProxy.CobrarAsync` devuelve `ACCESO_DENEGADO` sin invocar al Decorator ni al proveedor. En la interfaz, `consulta.demo` recibe la denegación y el pedido sigue VALIDADO (Figura 15).
- **Prueba:** `P22_roles_sin_permiso_no_alcanzan_la_pasarela` (BODEGA, CONSULTA), `P22_ventas_y_admin_son_autorizados`, `P22_rol_consulta_no_alcanza_decorator_ni_adapter_y_queda_auditoria_de_seguridad`.
- **Beneficio / costo:** la autorización no depende de botones ocultos; costo: una capa más en cada cobro.

### 13. Chain of Responsibility — validación del pedido
- **Problema:** las validaciones (cliente activo → líneas y cantidades → stock → autorización/condiciones) deben ejecutarse en orden, cortar en el primer rechazo y mostrar el recorrido.
- **Participantes:** `EslabonValidacion` y sus 4 eslabones concretos (`ClienteActivoValidador`, `LineasValidador`, `StockValidador`, `CondicionesConfirmacionValidador`); `CadenaValidacionPedido` arma la cadena.
- **Recorrido:** Carrito → «Validar pedido» → `ServicioPedidos.ValidarAsync` → cadena; la Facade vuelve a ejecutarla antes de cobrar. KIT_TELETRABAJO ×6 se detiene en el eslabón de stock.
- **Prueba:** 4 pruebas `P23_*`. **Evidencia:** el mensaje `[Reglas de stock] Stock insuficiente de P02: se requieren 6 y hay 4`.
- **Beneficio / costo:** cada regla es una clase pequeña que se reordena o amplía sin tocar las demás; costo: el orden de la cadena es una decisión que debe probarse.

### 14. Command — operaciones de negocio
- **Problema:** separar la interfaz de los casos de uso y registrar entrada y resultado de cada operación, sea desde un botón o desde una prueba.
- **Participantes:** `IComandoNegocio`; base `ComandoConEventos` (ejecutar receptor → despachar Outbox solo si hubo éxito); comandos concretos; `InvocadorComandos` (traduce excepciones, historial, auditoría); `FabricaComandos`.
- **Recorrido:** Cobro, Despacho, Pedidos y Devoluciones piden el comando a `FabricaComandos` → `InvocadorComandos.EjecutarAsync` registra la entrada, ejecuta, guarda el resultado en el historial y lo audita (pestaña «Comandos»).
- **Prueba:** `P24_invocador_ejecuta_y_registra_entrada_y_resultado_incluidos_errores`; `R08_*`, `R09_*`.
- **Beneficio / costo:** un único punto de trazabilidad y manejo de errores; costo: una clase por operación.

### 15. Interpreter — filtro de pedidos
- **Problema:** consultar pedidos con condiciones combinadas sin construir SQL a partir del texto del usuario.
- **Gramática:** `expr := term (OR term)*`, `term := factor (AND factor)*`, `factor := '(' expr ')' | ESTADO '=' valor | CLIENTE '=' texto | TOTAL op número`, `op ∈ {=, >, <, >=, <=}`. AND tiene mayor precedencia que OR; los paréntesis la alteran.
- **Participantes:** léxico y parser de `InterpreteFiltro`; árbol `IExpresionFiltro` (`ExpresionY`, `ExpresionO`, comparaciones de estado, cliente y total).
- **Recorrido:** Pedidos → escribir el filtro → `ConsultasOperativas` llama a `InterpreteFiltro.Filtrar`, que interpreta el árbol en memoria sobre los DTO; un error muestra la posición exacta.
- **Prueba:** `P25_*` (2), `P26_*` (9, incluida la inyección `TOTAL >= 50; DROP TABLE Pedidos` rechazada).
- **Beneficio / costo:** lenguaje seguro y extensible; costo: mantener léxico y parser (el archivo más grande de producción, 316 líneas).

### 16. Iterator — componentes físicos de un kit
- **Problema:** recorrer los SKU finales de un kit anidado sin exponer su estructura interna.
- **Participantes:** `IIteradorComponentes` / `IteradorComponentesFisicos` (pila explícita, sin exponer la colección interna) entrega cada `ProductoSimple` una vez por unidad requerida.
- **Recorrido:** `ExpansionSku.Expandir` lo obtiene con `CrearIterador(cantidad)`; lo usan la cadena de validación (stock), la calculadora (unidades para MAYORISTA) y `Pedido` (instantánea de componentes y descuento de stock por SKU final).
- **Prueba:** `P27_iterador_entrega_cada_sku_una_vez_por_unidad_y_termina`, `P27_iterador_propaga_cantidades_de_forma_determinista`.
- **Beneficio / costo:** los consumidores no conocen la profundidad del kit; costo: un tipo iterador propio en lugar de `IEnumerable`.

### 17. Mediator — `FormularioPrincipalMediator`
- **Problema:** tras cada operación varias pantallas deben refrescarse; si se llamaran entre sí, cada pantalla conocería a todas.
- **Participantes:** mediador con tabla evento → pantallas a invalidar; `IColegaPantalla`; `PantallaBase` con recarga diferida (solo recarga la visible).
- **Recorrido:** tras cobrar, `PantallaCobro` notifica `PedidoActualizado` → el mediador marca como desactualizadas Pedidos, Cobro, Despacho, Devoluciones, Panel y Auditoría → cada una se recarga al mostrarse.
- **Prueba:** `P28_*` (3), incluida la verificación por reflexión de que ninguna pantalla referencia a otra.
- **Beneficio / costo:** pantallas desacopladas; costo: la tabla de eventos del mediador debe mantenerse al añadir pantallas.

### 18. Memento — deshacer / rehacer del borrador
- **Problema:** editar el carrito sin perder trabajo por un error y sin exponer el estado interno del borrador.
- **Participantes:** originador `PedidoBorrador` (`CrearMemento`, `Restaurar`), memento inmutable `BorradorMemento` (cliente, entrega y líneas anidadas), cuidador `HistorialBorrador` (máx. 5, se limpia y bloquea al validar).
- **Recorrido:** Carrito → cada edición guarda un memento → «Deshacer» / «Rehacer» restauran el estado exacto; al validar, el historial se vacía y los botones se deshabilitan.
- **Prueba:** `P29_tres_ediciones_dos_deshacer_un_rehacer_restauran_estados_exactos`, `P29_historial_conserva_como_maximo_cinco_estados`, `P29_al_confirmar_el_historial_se_limpia_y_no_permite_deshacer`.
- **Beneficio / costo:** deshacer sin romper el encapsulamiento; costo: memoria de hasta 5 copias del borrador.

### 19. Observer — eventos de dominio vía Outbox
- **Problema:** avisar al cliente, auditar y actualizar métricas tras un cambio de negocio sin acoplar el caso de uso a esos efectos ni perderlos si fallan.
- **Participantes:** sujeto `PublicadorEventos`; observadores `NotificacionClienteObserver`, `AuditoriaNegocioObserver` y `MetricasObserver`; `ProcesadorOutbox` publica solo después del commit y registra por evento qué observadores ya lo atendieron.
- **Recorrido:** el comando confirma → `ComandoConEventos` llama a `ProcesarPendientesAsync` → `PublicarAsync` invoca a cada observador pendiente; si uno falla, el evento queda PENDIENTE y «Reprocesar Outbox» lo completa una sola vez.
- **Prueba:** `P30_*` (2) y `P37_falla_del_notificador_deja_el_evento_pendiente_y_se_recupera_una_sola_vez`.
- **Beneficio / costo:** nuevos efectos se suscriben sin tocar casos de uso y no se pierden; costo: consistencia eventual entre el commit y los avisos.

### 20. State — ciclo del pedido
- **Problema:** cada estado admite acciones distintas; un `switch` repetido en cada operación sería frágil.
- **Participantes:** `EstadoPedido` (todas las acciones rechazadas por defecto) y 6 estados concretos que solo sobrescriben sus transiciones válidas; `Pedido` delega en el estado y registra el historial.
- **Recorrido:** `Pedido.Validar`, `Despachar`, `Entregar` y `Cancelar` delegan en `Estado`; una transición no permitida (p. ej. cancelar un PAGADO) produce `TRANSICION_INVALIDA` y el historial no cambia.
- **Prueba:** `P31_*` (4), `P08_*`.
- **Beneficio / costo:** las reglas de cada estado están en un solo lugar; costo: una clase por estado.

### 21. Strategy — promociones excluyentes
- **Problema:** las promociones cambian con el negocio y nunca se acumulan; MAYORISTA tiene prioridad sobre VIP.
- **Participantes:** `IPoliticaDescuento` (`Codigo`, `Prioridad`, `Tasa`, `Aplica`), `PoliticaSinDescuento`, `PoliticaVip10`, `PoliticaMayorista15`; `SelectorPoliticaDescuento` elige la aplicable de mayor prioridad (nunca acumula).
- **Recorrido:** Carrito → `ServicioPedidos.Valorar` → `CalculadoraPedido` → `SelectorPoliticaDescuento` elige VIP para Ana (−6.00) y MAYORISTA para Carmen (−15.00, 10 unidades físicas).
- **Prueba:** `P32_*` (5), incluida `P32_nueva_promocion_se_agrega_sin_modificar_la_calculadora`.
- **Beneficio / costo:** una promoción nueva es una clase registrada en DI (OCP); costo: la prioridad entre políticas debe documentarse.

### 22. Template Method — devoluciones
- **Problema:** toda devolución sigue la misma secuencia, pero cada motivo tiene plazos y condiciones distintos.
- **Participantes:** clase base abstracta `ProcesarDevolucion` (método plantilla `ProcesarAsync` y pasos protegidos), subclases `DevolucionDefecto` y `DevolucionArrepentimiento`; `ServicioDevoluciones` elige la variante según el motivo.
- **Secuencia invariante (`ProcesarAsync`):** 1. verificar elegibilidad → 2. inspeccionar condición → 3. calcular reembolso → 4. registrar devolución y stock → 5. generar evento. `DevolucionDefecto` (≤ 30 días, reintegra stock) y `DevolucionArrepentimiento` (≤ 7 días y sin uso) personalizan los pasos permitidos.
- **Recorrido:** Devoluciones → elegir PED-001 y motivo Defecto → `ProcesarDevolucionCommand` → `ProcesarAsync` → DEV-001, reembolso USD 54.00 sin envío, reintegro único de stock y evento `DevolucionAceptada`.
- **Prueba:** `P33_*` (2), `P35_devolucion_de_ana_por_defecto_reembolsa_54_sin_flete_una_sola_vez`.
- **Beneficio / costo:** la secuencia y sus garantías no se duplican; costo: las subclases solo pueden variar los pasos previstos.

### 23. Visitor — desglose y peso logístico
- **Problema:** obtener el desglose físico y el peso de un kit sin añadir esos reportes a las clases del catálogo.
- **Participantes:** `IVisitanteCatalogo` con `VisitarProducto` / `VisitarKit`; `Aceptar` (doble despacho) en `ProductoSimple` y `KitCompuesto`; `DesgloseSkuVisitor` (SKU por nivel) y `PesoLogisticoVisitor` (kg).
- **Recorrido:** Catálogo → seleccionar KIT_TELETRABAJO → `ConsultaCatalogoVisual` hace `kit.Aceptar(desglose)` y `kit.Aceptar(peso)` → la pantalla muestra «P01 ×1, P02 ×1, P03 ×1», «1.00 kg» y los kits visitados KIT_TELETRABAJO → KIT_OFICINA.
- **Prueba:** `P34_visitante_de_desglose_enumera_skus_y_visita_kits_anidados`, `P34_visitante_de_peso_calcula_un_kilo_para_kit_teletrabajo` (1,00 kg), `P34_P21_la_pantalla_de_catalogo_*`.
- **Beneficio / costo:** nuevos reportes sobre el catálogo sin tocar el Composite; costo: añadir un tipo de elemento obliga a ampliar todos los visitantes.
