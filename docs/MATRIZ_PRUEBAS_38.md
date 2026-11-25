# Matriz de 38 pruebas (P01–P38) — resultados obtenidos

- **Automatizadas:** `dotnet test -c Release` → **98 aprobadas / 98 ejecutadas, 0 fallidas, 0 omitidas** (log completo en
  `docs/evidencias/pruebas/resultado_pruebas.txt`, ejecución del 25/11/2026 00:03, SDK .NET 8.0.428, Windows 11 26200).
- **Manuales en la interfaz real:** sesión automatizada con UI Automation sobre `LogiMarket360.exe` (Release) el
  24/11/2026 entre 23:44 y 23:53, base de datos nueva en carpeta aislada (`--datos`). Capturas en `docs/evidencias/capturas/`;
  script reproducible en `docs/evidencias/automatizacion_ui.ps1`.
- Archivo de pruebas: `tests/LogiMarket.Tests/` — `Integracion/FuncionalidadReglasTests.cs` (FR), `Integracion/PatronesIntegracionTests.cs` (PI),
  `Integracion/IntegridadRecuperacionTests.cs` (IR), `Dominio/*` (D), `Aplicacion/*` (A), `Presentacion/PresentacionTests.cs` (UI),
  `Arquitectura/ReglasDeDependenciaTests.cs` (AR).
- Las pruebas de rechazo verifican además la **ausencia de efectos colaterales** (stock, pagos, Outbox, LOG de cobro).

## A. Funcionalidad y reglas

| ID | Entrada / precondición | Esperado | Obtenido | Evidencia | Fecha | Archivo / método | Resultado |
|---|---|---|---|---|---|---|---|
| P01 | Ruta de BD inexistente; iniciar dos veces | Migración aplicada, 5 productos, 2 kits, 4 usuarios con hash PBKDF2, 4 clientes; sin duplicados al relanzar | BD creada desde cero; conteos 5/2/4/4 idénticos tras la segunda inicialización | `01_acceso.png`, `02_panel_inicial.png` | 25/11/2026 | FR `P01_bd_desde_archivo_inexistente_migra_siembra_y_no_duplica_al_relanzar` | ✅ |
| P02 | Ana (VIP) · KIT_TELETRABAJO ×1 · Estándar | Subtotal 60.00, desc. VIP 6.00, envío 5.00, **total 59.00**; stock P01=7, P02=4, P03=19; PAGADO; 1 pago | Total USD 59.00; stock 7/4/19; PED-001 PAGADO con LEG-000001; 1 pago | `05_carrito_P02_ana.png`, `07_cobro_P02_ana.png` | 24/11 (UI) · 25/11 (auto) | FR `P02_ana_vip_kit_teletrabajo_estandar_total_59_y_stock_7_4_19` | ✅ |
| P03 | Luis (NORMAL) · P04 ×2 + P05 ×2 · Exprés | **Total 102.00**; P04=2, P05=28 | USD 102.00 (80 + 10 + 12); P04=2; P05=28 | `05_carrito_P03_luis.png`, `07_cobro_P03_luis.png` | 24/11 · 25/11 | FR `P03_luis_normal_expres_total_102` | ✅ |
| P04 | Carmen (NORMAL) · P03 ×10 (volumen mayorista) · Estándar | Desc. MAYORISTA 15.00, **total 90.00**; P03=10 | USD 90.00; descuento 15.00; P03 = 10 | `05_carrito_P04_carmen.png`, `07_cobro_P04_carmen.png` | 24/11 · 25/11 | FR `P04_carmen_mayorista_por_volumen_total_90` | ✅ |
| P05 | KIT_TELETRABAJO ×6 | Rechazo por P02 (se requieren 6, hay 4); sin pago, stock ni evento | `[Reglas de stock] Stock insuficiente de P02: se requieren 6 y hay 4` · código `STOCK_INSUFICIENTE`; conteos sin cambio | `05_validacion_P05_stock_insuficiente.png` | 24/11 · 25/11 | FR `P05_kit_teletrabajo_por_6_se_rechaza_por_p02_sin_efectos`, `P05b_*` | ✅ |
| P06 | Modo demostración «Inducir rechazo de cobro» | Pedido sigue VALIDADO; stock intacto; sin pago; sin Outbox de éxito | PED-004 → `COBRO_RECHAZADO`, sigue VALIDADO; 0 pagos y 0 eventos nuevos | `07_cobro_P06_rechazo_inducido.png` | 24/11 · 25/11 | FR `P06_error_de_cobro_simulado_deja_todo_intacto_sin_outbox_de_exito` | ✅ |
| P07 | Confirmar dos veces el mismo pedido (misma clave de idempotencia) | 1 pago, 1 movimiento por SKU, resultado original | Segunda confirmación: «YA COBRADO (repetición idempotente)» con la misma referencia LEG-000001 | `07_cobro_P07_repeticion_idempotente.png` | 24/11 · 25/11 | FR `P07_repetir_confirmacion_con_la_misma_clave_devuelve_el_resultado_original_sin_cobro_doble` | ✅ |
| P08 | Pedido PAGADO: cancelar y modificar; cambiar precio del catálogo | Ambas operaciones denegadas; el pedido conserva su instantánea de precio | `TRANSICION_INVALIDA` en ambas; total sin cambios tras cambiar el precio del producto | `06_pedidos_historial_PED001.png` (Cancelar deshabilitado) | 25/11/2026 | FR `P08_pedido_pagado_no_se_cancela_ni_se_modifica_y_conserva_su_precio` | ✅ |
| P09 | BODEGA despacha y entrega; despachar un pedido no pagado | Secuencia PAGADO → DESPACHADO → ENTREGADO; despacho sin pago rechazado | GUIA-2026-0001 y GUIA-2026-0002; ambos ENTREGADO; VALIDADO no se despacha | `08_despacho_*.png`, `08_entregados_P09.png` | 24/11 · 25/11 | FR `P09_bodega_despacha_y_entrega_en_secuencia_y_no_despacha_sin_pago` | ✅ |
| P10 | Cerrar y reabrir la aplicación | Pedido, stock, pago y auditoría persisten | Tras reiniciar: PED-001 ENTREGADO con LEG-000001 y GUIA-2026-0001, devolución de USD 54.00, stock y movimientos iguales | `02_panel_P10_tras_reinicio.png`, `06_pedidos_P10_tras_reinicio.png`, `10_inventario_P10_tras_reinicio.png` | 24/11 · 25/11 | FR `P10_cerrar_y_reabrir_conserva_pedido_stock_pago_y_auditoria` | ✅ |

## B. Creacionales

| ID | Entrada / precondición | Esperado | Obtenido | Evidencia | Fecha | Archivo / método | Resultado |
|---|---|---|---|---|---|---|---|
| P11 | PedidoPagado de Ana (EMAIL) y de Luis (SMS) | Cada creador devuelve su notificador; aviso registrado en el canal correcto; WinForms no instancia concretos | EMAIL a `ana.lopez@demo.local` (PED-001) y SMS a `0990000002` de Luis (PED-002) | `12_notificaciones_P11.png` | 24/11 · 25/11 | PI `P11_factory_method_elige_email_o_sms_segun_el_cliente_y_registra_el_aviso` | ✅ |
| P12 | Familias Estándar y Exprés | Tarifa 5/12, plazo 3/1 días, etiqueta coherente; no mezclar familias | Valores exactos; mezcla rechazada | `08_despacho_detalle_PED-001.png` / `_PED-002.png` | 25/11/2026 | D `P12_*` (3) | ✅ |
| P13 | Pedido vacío, sin cliente, sin líneas; pedido completo | Rechazos; el completo crea un agregado válido multirrenglón | Rechazos con mensaje; agregado con instantáneas | — | 25/11/2026 | D `P13_*` (3) | ✅ |
| P14 | Clonar borrador con KIT_TELETRABAJO y editar la copia | Plantilla intacta; colecciones con referencia distinta; sin id ni pago | Copia profunda independiente; el clon vuelve a ser editable | — | 25/11/2026 | D `P14_*` (2) | ✅ |
| P15 | Resolver `MetricasSesion` desde distintos ámbitos e hilos | Misma instancia; contadores coherentes | `BeSameAs` en ámbitos y en la ventana; incrementos concurrentes exactos | Barra de estado `MetricasSesion #023F19A8` en todas las pantallas | 24/11 · 25/11 | PI `P15_*`; UI `Composition_root_*` | ✅ |

## C. Estructurales

| ID | Entrada / precondición | Esperado | Obtenido | Evidencia | Fecha | Archivo / método | Resultado |
|---|---|---|---|---|---|---|---|
| P16 | Cobrar con LEGACY y con V2 | Mismo contrato y resultado; la aplicación no conoce `CobrarCentavos`; legado intacto | Ambos aprueban; LEGACY traduce `CobrarCentavos(5900) → 00|LEG-000001` | `12_auditoria_pagos_P19.png` | 24/11 · 25/11 | PI `P16_adapter_legado_y_proveedor_v2_cumplen_el_mismo_contrato` | ✅ |
| P17 | Venta/Devolución × VistaPrevia/TXT | 4 combinaciones; TXT real; vista previa visible | 4 combinaciones; archivo TXT real generado | `11_reportes_*.png`, `Comprobante_de_devolución_DEV-001_*.txt` | 24/11 · 25/11 | PI `P17_bridge_cuatro_combinaciones_con_txt_real_y_vista_previa_visible` | ✅ |
| P18 | Precios de kits; expansión; ciclo directo e indirecto | KIT_OFICINA=50, KIT_TELETRABAJO=60; SKU finales; ciclo rechazado | 50.00 / 60.00; P01, P02, P03; `KIT_CICLICO` sin modificar la composición | `03_catalogo_*.png` | 24/11 · 25/11 | D `P18_*` (4) | ✅ |
| P19 | Cobro aprobado y cobro rechazado | INICIO → PASARELA → FIN/ERROR con la misma correlación | `COR-49c9…`: COBRO INICIO → PASARELA LEGACY → COBRO FIN; rechazo con su propio `COR-` | `resultado_pruebas.txt`, `12_auditoria_pagos_P19.png` | 24/11 · 25/11 | PI `P19_decorator_registra_inicio_pasarela_y_fin_con_la_misma_correlacion` | ✅ |
| P20 | Falla de un colaborador tras el cobro | Sin persistencia parcial; cobro compensado | Pedido sigue VALIDADO; reverso del cobro registrado; sin stock ni Outbox | — | 25/11/2026 | PI `P20_facade_con_falla_en_un_colaborador_no_deja_persistencia_parcial_y_compensa_el_cobro` | ✅ |
| P21 | 10 000 renglones de vista (requisito ≥ 1 000) | Metadatos compartidos; comparación en condiciones equivalentes | 5 instancias frente a 10 000; 1 600 536 B frente a 5 040 256 B (−68,2 %) | `resultado_pruebas.txt` | 25/11/2026 | PI `P21_flyweight_comparte_metadatos_entre_mil_filas_y_reduce_memoria_en_escenario_equivalente` | ✅ |
| P22 | CONSULTA y BODEGA intentan cobrar | Denegado sin invocar pasarela ni LOG de cobro; VENTAS/ADMIN autorizados | `ACCESO_DENEGADO`; 0 llamadas a la pasarela; auditoría SEGURIDAD; métrica «cobros denegados» = 1 | `07_cobro_P22_consulta_denegado.png` | 24/11 · 25/11 | A `P22_*` (4); PI `P22_rol_consulta_*` | ✅ |

## D. Comportamentales

| ID | Entrada / precondición | Esperado | Obtenido | Evidencia | Fecha | Archivo / método | Resultado |
|---|---|---|---|---|---|---|---|
| P23 | Pedido sin cliente y sin stock; luego corregido | Se detiene en el primer eslabón; al corregir se alcanza el siguiente; sin cobro | Recorrido «Cliente activo» → corregido → «Reglas de stock» | `05_validacion_P05_stock_insuficiente.png` | 24/11 · 25/11 | A `P23_*` (4) | ✅ |
| P24 | Misma acción desde botón y desde prueba | Sin lógica duplicada; historial con entrada y resultado | Historial de comandos con entrada, código y mensaje | `12_comandos_P24.png` | 24/11 · 25/11 | A `P24_invocador_ejecuta_y_registra_entrada_y_resultado_incluidos_errores` | ✅ |
| P25 | Interfaz: `ESTADO = PAGADO AND TOTAL > 60`; prueba: `ESTADO = PAGADO AND TOTAL >= 50` | Solo los pedidos que cumplen ambas condiciones | Interfaz: PED-003 (USD 90.00) y PED-002 (USD 102.00); PED-001 (USD 59.00) excluido. Prueba: PED-001 y PED-002 | `06_pedidos_filtro_P25.png` | 24/11 · 25/11 | A `P25_*` (2) | ✅ |
| P26 | `TOTAL > AND …`, paréntesis, inyección | Error legible; precedencia correcta | «Error de sintaxis en la posición 9: se esperaba un número después de TOTAL; se encontró 'AND'.» | `06_pedidos_filtro_invalido_P26.png` | 24/11 · 25/11 | A `P26_*` (9) | ✅ |
| P27 | Iterar KIT_TELETRABAJO | P01, P02, P03 una vez por unidad; termina | Secuencia determinista y finita | — | 25/11/2026 | D `P27_*` (2) | ✅ |
| P28 | Confirmar desde Cobro / validar desde Carrito | Pantallas refrescadas vía mediador; sin acoplamiento directo | Catálogo, pedidos, inventario, panel y despacho invalidados; Clientes no; navegación a Cobro | Flujo `05_validacion_*` → `07_cobro_*` | 24/11 · 25/11 | UI `P28_*` (3) | ✅ |
| P29 | 3 ediciones, 2 deshacer, 1 rehacer | Borradores exactos; no se deshace un pago | Estados exactos; historial «2/5 estados · rehacer disponible» | `05_carrito_P29_*.png` | 24/11 · 25/11 | D `P29_*` (3) | ✅ |
| P30 | PedidoPagado con 3 suscriptores; reintento | Una atención por suscriptor; sin duplicados | 1/1/1; reintento sin duplicar | — | 25/11/2026 | A `P30_*` (2) | ✅ |
| P31 | Ciclo completo; PAGADO→CANCELADO; estado repetido | Ciclo válido; rechazos | Historial completo; `TRANSICION_INVALIDA` | `06_pedidos_historial_PED001.png` | 24/11 · 25/11 | D `P31_*` (4) | ✅ |
| P32 | Ana, Carmen, Luis; política nueva | 6.00 / 15.00 / 0.00; no acumula; promoción nueva sin tocar la calculadora | Valores exactos; prioridad explícita | `05_carrito_P0x_*.png` | 24/11 · 25/11 | D `P32_*` (5) | ✅ |
| P33 | DEFECTO día 20; ARREPENTIMIENTO día 10; producto usado | Defecto aceptado; arrepentimiento rechazado sin reembolso ni stock | `FUERA_DE_PLAZO` y `PRODUCTO_USADO`; defecto con 5 pasos | `09_devolucion_P33_arrepentimiento_usado_rechazado.png` | 24/11 · 25/11 | IR `P33_*` (2) | ✅ |
| P34 | Visitar KIT_TELETRABAJO | Desglose P01/P02/P03; peso 1,00 kg | Desglose por nivel; 1,00 kg | `03_catalogo_arbol_kit_teletrabajo.png` | 25/11/2026 | D `P34_*` (2) | ✅ |

## E. Integridad, recuperación y arquitectura

| ID | Entrada / precondición | Esperado | Obtenido | Evidencia | Fecha | Archivo / método | Resultado |
|---|---|---|---|---|---|---|---|
| P35 | PED-001 de Ana ENTREGADO, devolución por DEFECTO; repetir | **USD 54.00** sin flete; un reintegro; devolución única | «Devolución DEV-001 aceptada. Reembolso USD 54.00 (sin envío).» Segundo intento rechazado; stock reintegrado una sola vez | `09_devolucion_P35_*.png`, TXT DEV-001 | 24/11 · 25/11 | IR `P35_devolucion_de_ana_por_defecto_reembolsa_54_sin_flete_una_sola_vez` | ✅ |
| P36 | Dos cobros concurrentes sobre SKU con stock = 1 | Como máximo un cobro aprobado; stock nunca negativo | PED-002 confirmado; PED-001 `CONFLICTO_CONCURRENCIA` (concurrencia optimista) con reverso del cobro | `resultado_pruebas.txt` | 25/11/2026 | IR `P36_dos_cobros_concurrentes_sobre_stock_1_aprueban_como_maximo_uno` | ✅ |
| P37 | Notificador caído; luego reprocesar | Evento PENDIENTE; al reprocesar se procesa una vez; sin cobro duplicado | Outbox 7 PENDIENTE (error NOTIFICACION) → «1 procesados, 0 pendientes»; 1 pago | `12_outbox_P37_pendiente.png`, `12_outbox_P37_procesado.png` | 24/11 · 25/11 | IR `P37_falla_del_notificador_deja_el_evento_pendiente_y_se_recupera_una_sola_vez` | ✅ |
| P38 | Reglas de dependencia, DIP, compilación limpia y ≥ 25 pruebas | Referencias prohibidas ausentes; build desde cero; ≥ 25 aprobadas | 6 pruebas de arquitectura aprobadas; la Facade solo depende de abstracciones; build desde el ZIP limpio sin errores; **98/98** aprobadas | `fase0_arquitectura_referencia_prohibida.log`, `pruebas/build_limpio.txt` | 25/11/2026 | AR (6); PI `P38_dip_la_facade_depende_solo_de_abstracciones_de_aplicacion` | ✅ |

## Resumen

| Grupo | Casos | Aprobados |
|---|---|---|
| A. Funcionalidad y reglas | P01–P10 | 10/10 |
| B. Creacionales | P11–P15 | 5/5 |
| C. Estructurales | P16–P22 | 7/7 |
| D. Comportamentales | P23–P34 | 12/12 |
| E. Integridad / recuperación / arquitectura | P35–P38 | 4/4 |
| **Total matriz** | **38** | **38/38** |
| Pruebas automatizadas (xUnit) | 98 | **98/98** (incluye 10 guardas de refactorización R01–R10) |
