# Riesgos de diseño y refactorizaciones (Actividad 7)

Este documento registra **10 riesgos reales** encontrados al construir LogiMarket 360 y al operar la aplicación en Windows.
Ninguno es un defecto inventado: cada uno se resolvió con un commit concreto, y su efecto se comprobó con una prueba
ejecutada **antes** (sobre el código previo) y **después** (sobre el código corregido). Las salidas literales de esas ejecuciones
están en `docs/evidencias/refactorizacion/`.

Cómo se obtuvo cada "antes":
- Si el defecto era observable, se escribió primero la prueba y se ejecutó contra el código anterior: falla.
- Si la refactorización conserva el comportamiento (R04, R06, R09), el "antes" es la medición del olor (literales, duplicados) y las mismas pruebas de contrato, que pasan en verde antes y después.

## Resumen

| ID | Riesgo (categoría) | Dónde se manifestó | Commit(s) | Prueba de guarda | Antes → después |
|---|---|---|---|---|---|
| R01 | Selección de grilla basada en `CurrentRow` (estado oculto de UI) | Despacho y Pedidos mostraban el detalle de otra fila | `d006d56`, prueba `cb191a0` | `R01_la_grilla_devuelve_la_fila_seleccionada_aunque_no_sea_la_fila_actual` | ❌ «PED-001» en vez de «PED-003» → ✅ |
| R02 | Efecto colateral omitido: métrica de cobros denegados | La barra de estado mostraba 0 cobros denegados tras una denegación a CONSULTA | `59d4ea7` | `P22_rol_consulta_no_alcanza_decorator_ni_adapter_y_queda_auditoria_de_seguridad` | ❌ esperado 2, obtenido 1 → ✅ |
| R03 | Retroalimentación visual insuficiente (usabilidad) | Botones primarios deshabilitados seguían pareciendo activos | `d006d56`, prueba `cb191a0` | `R03_un_boton_primario_deshabilitado_cambia_de_aspecto_y_lo_recupera_al_habilitarse` | ❌ → ✅ |
| R04 | Obsesión por primitivos: literales de estado dispersos | 21 literales `"PAGADO"`, `"VALIDADO"`… en Application y WinForms; 3 listas de estados duplicadas | `cdbf622` | `R04_los_nombres_de_estado_expuestos_coinciden_con_los_objetos_State_del_dominio`, `R04_el_interprete_acepta_exactamente_los_estados_del_dominio` | 21 → 0 literales; pruebas de contrato verdes |
| R05 | Formato monetario duplicado y dependiente de la cultura | Con cultura es-EC la auditoría registraba «USD 59,00» | `391d335` | `R05_los_importes_en_auditoria_usan_punto_decimal_aunque_la_cultura_sea_es_EC` | ❌ «Cobro USD 59,00…» → ✅ «USD 59.00» |
| R06 | Código de error mágico (`"ACCESO_DENEGADO"`) repetido en 3 capas | Invocador, cadena de validación y ejecutor de UI | `09ce800` | `P22_*`, `P23_*` (contrato del código) | 3 literales → 1 constante; pruebas verdes |
| R07 | Dependencia oculta del reloj del sistema en consultas | La alerta «entregados dentro del plazo» ignoraba el `IReloj` de la aplicación | `3ed0b35` | `R07_la_alerta_de_devoluciones_por_revisar_usa_el_reloj_de_la_aplicacion` | ❌ alerta ausente → ✅ |
| R08 | Violación de OCP: el invocador preguntaba por un tipo concreto | `if (comando is ConfirmarPedidoCommand)` introducido por R02 | `bfda083` | `R08_cada_comando_decide_que_metrica_afecta_su_denegacion_sin_que_el_invocador_conozca_tipos_concretos` | ❌ esperado 1, obtenido 0 → ✅ |
| R09 | Lógica duplicada e inconsistente de postconfirmación del Outbox | 3 comandos repetían «si hubo éxito, despachar Outbox», cada uno con una condición distinta | `d2dfe74` | `R09_un_comando_con_eventos_solo_despacha_el_outbox_cuando_el_receptor_tuvo_exito` + `P24`, `P30`, `P35`, `P37`, `P09` | 3 llamadas → 1; suite 97/97 → 98/98 |
| R10 | Reloj del sistema en los simuladores de Infrastructure | Referencias `V2-20261125-…` y `RMB-…` no reproducibles | `a3216ab` | `R10_las_referencias_simuladas_de_cobro_V2_y_reembolso_usan_el_reloj_de_la_aplicacion` | ❌ «V2-20261125-0001» → ✅ «V2-20260110-…» |

---

## Detalle

### R01 — Selección de grilla basada en `CurrentRow`
- **Síntoma observado:** durante la sesión de capturas, al seleccionar PED-002 en Despacho el panel de detalle seguía mostrando PED-001, y las acciones apuntaban al pedido equivocado. Pasa cuando la selección no mueve la celda actual (selección programática o por accesibilidad/UI Automation).
- **Causa:** `GrillaDatos.Seleccionado<T>()` devolvía `CurrentRow`.
- **Refactorización:** usar `SelectedRows[0]` y recurrir a `CurrentRow` solo si no hay selección, tanto en `Seleccionado` como al conservar la selección en `Mostrar`.
- **Antes/después:** `refactorizacion/R01_R03_antes.txt` (falla: «Expected … "PED-003", but "PED-001"») y `R01_R03_despues.txt` (aprobada).

### R02 — Métrica de cobros denegados no contabilizada
- **Síntoma observado:** CONSULTA intentó cobrar y recibió `ACCESO_DENEGADO`, pero la barra de estado seguía mostrando «0 cobros denegados».
- **Causa:** la Facade verifica el permiso antes de llegar al Proxy, y solo el Proxy registraba la métrica.
- **Refactorización:** contabilizar la denegación en el camino del comando de confirmación (su diseño final es R08).
- **Antes/después:** `R02_antes.txt` («expected … to be 2 … but found 1») y `R02_despues.txt`.

### R03 — Botones deshabilitados con aspecto activo
- **Síntoma observado:** en las capturas, «Confirmar cobro» y «Marcar como despachado» se veían azules aunque estaban deshabilitados. Con `FlatStyle.Flat` y colores explícitos, WinForms no atenúa el fondo.
- **Refactorización:** `Tema.Boton` reacciona a `EnabledChanged` con fondo gris, texto suave y borde neutro, y restaura el estilo original al habilitarse.
- **Antes/después:** `R01_R03_antes.txt` / `R01_R03_despues.txt`. Visible en `08_despacho_detalle_PED-001.png` («Registrar entrega» deshabilitado).

### R04 — Literales de estado dispersos (obsesión por primitivos)
- **Riesgo:** 21 literales en Application y WinForms, más tres listas de estados duplicadas (`ConsultasOperativas.EstadosActivos`, `InterpreteFiltro.Estados`, `PantallaPedidos.Flujo`). Un estado nuevo o renombrado habría desincronizado el filtro, el panel y los chips sin ningún error de compilación.
- **Refactorización:** `D/Pedidos/Estados/NombresEstado.cs` es la fuente única, con `Todos`, `Flujo` y `Activos`. Una prueba garantiza que coincide con `EstadoPedido.De(c).Nombre` de los objetos State.
- **Medición:** `refactorizacion/R04_literales_estado.txt` (21 → 0).

### R05 — Formato monetario dependiente de la cultura
- **Riesgo:** 8 sitios de producción formateaban importes por su cuenta (`{monto:0.00}` o `ToString("0.00")`). Los que usaban interpolación dependían de la cultura del hilo: en un Windows configurado en español (Ecuador), la auditoría, las notificaciones y el Decorator registraban «USD 59,00», inconsistente con la interfaz y los comprobantes («USD 59.00»).
- **Refactorización:** `Dinero.Formatear(decimal)` en el dominio (redondeo comercial + `InvariantCulture`), usado por `Dinero.ToString`, `Tema.Usd`, `Comprobante.Usd`, los observadores, el Proxy, el Decorator y los simuladores.
- **Antes/después:** `R05_antes.txt` («"Cobro USD 59,00 clave PED-001-K" to contain "USD 59.00"») y `R05_despues.txt`.

### R06 — Código de error mágico
- **Riesgo:** `"ACCESO_DENEGADO"` estaba escrito a mano en el invocador, en el último eslabón de la cadena y en la UI, que lo compara para elegir el icono de los mensajes. Un error tipográfico habría roto la UI sin fallar la compilación.
- **Refactorización:** la constante `AccesoDenegadoException.Codigo` se usa en las tres capas.
- **Antes/después:** `R06_codigo_acceso.txt` (3 literales → 1 definición; pruebas P22/P23 verdes).

### R07 — Reloj del sistema en las consultas del panel
- **Riesgo:** `ConsultasOperativas.PanelAsync` usaba `DateTime.UtcNow` mientras el resto de Application usa `IReloj`. Las pruebas de plazos (P33) y el panel no compartían la misma noción del tiempo.
- **Refactorización:** se inyecta `IReloj` en `ConsultasOperativas`.
- **Antes/después:** `R07_antes.txt` (la alerta «1 pedido(s) entregado(s) dentro del plazo» no aparece con un reloj de enero) y `R07_despues.txt`.

### R08 — Comprobación de tipo concreto en el invocador (OCP)
- **Riesgo:** la corrección rápida de R02 introdujo `if (comando is ConfirmarPedidoCommand)` en `InvocadorComandos`. Cada comando nuevo con métricas propias habría obligado a modificar el invocador.
- **Refactorización:** `IComandoNegocio.AlDenegarseAcceso(MetricasSesion)` es un gancho con implementación vacía por defecto; `ConfirmarPedidoCommand` lo sobrescribe y el invocador ya no conoce tipos concretos.
- **Antes/después:** `R08_antes.txt` (comando de prueba de cobro: esperado 1, obtenido 0) y `R08_despues.txt` (R08 y P22 aprobadas).

### R09 — Postconfirmación del Outbox duplicada
- **Riesgo:** `ConfirmarPedidoCommand`, `DespacharPedidoCommand` y `ProcesarDevolucionCommand` repetían el despacho del Outbox, cada uno con una condición distinta (Despachar no comprobaba el resultado). Un comando futuro podía olvidar despachar los eventos o hacerlo tras un rechazo, lo que contradice «sin efectos colaterales en rechazos».
- **Refactorización:** la base `ComandoConEventos` fija la secuencia «receptor → si `Exito` y hubo eventos nuevos, despachar el Outbox». Los comandos solo aportan `EjecutarReceptorAsync`; la repetición idempotente declara que no generó eventos.
- **Antes/después:** `R09_postconfirmacion_comandos.txt` (3 llamadas → 1; P24, P30, P35, P37 y P09 verdes; nueva guarda R09).

### R10 — Reloj del sistema en los simuladores de pago
- **Riesgo:** `PasarelaV2Simulada` y `ServicioReembolsoSimulado` generaban sus referencias con `DateTime.UtcNow`. Con un reloj de prueba, el pago o el reembolso llevaban otra fecha, y en Ecuador (UTC−5) a partir de las 19:00 la referencia ya mostraba el día siguiente.
- **Refactorización:** ambos simuladores reciben `IReloj`.
- **Antes/después:** `R10_antes.txt` («"V2-20261125-0001" differs…») y `R10_despues.txt`.

---

## Otros fallos detectados durante la construcción (corregidos antes de su commit)

Se registran por trazabilidad. No tienen un "antes" en Git porque se corrigieron en la misma sesión de trabajo en que aparecieron.

| Fallo | Detección | Corrección |
|---|---|---|
| Migración inicial con marca de tiempo posterior a la fecha del commit (orden de migraciones incoherente) | Revisión del historial | Migración renombrada a `20261122150500_Inicial` |
| P19 agrupaba 3 correlaciones: la Facade registra su propio «COBRO RECHAZADO» con el código del pedido | Prueba P19 en rojo | La prueba filtra las correlaciones `COR-` y verifica aparte la traza con `PED-` |
| La clase `Pantallas` chocaba con el espacio de nombres `LogiMarket.WinForms.Pantallas` | Error de compilación | Renombrada a `ClavesPantalla` |
| La propiedad `DialogoKit.Nombre` ocultaba un miembro heredado (advertencia de ocultación) | Advertencia del compilador | Renombrado a `NombreKit` |
| Una recarga solicitada durante otra carga se perdía (el selector de Cobro quedaba vacío tras pagar) | Sesión de capturas | `PantallaBase` repite la carga mientras siga marcada como desactualizada |
| Vista previa de comprobantes truncada y ruta absoluta del TXT expuesta en pantalla | Capturas `11_reportes_*` | Panel más ancho y solo el nombre del archivo (`16abd7c`) |

## Revisión de antipatrones (estado final)

| Antipatrón | Verificación | Estado |
|---|---|---|
| SQL o EF en Domain/Application | `ReglasDeDependenciaTests` (7 pruebas): Domain sin referencias; Application solo Domain | Ausente |
| Formularios que se llaman entre sí | `P28_ninguna_pantalla_referencia_a_otra_pantalla` (reflexión) | Ausente |
| Cálculo de tarifas en formularios | Tarifas solo en `FamiliasEntrega` (Abstract Factory) | Ausente |
| Clase Dios | Archivo más grande de producción: `InterpreteFiltro.cs` (316 líneas, léxico + parser + AST); Facade de 159 líneas que solo coordina | Controlado |
| `switch` gigante de estados | State con 6 clases; `CodigoEstadoPedido` solo identifica | Ausente |
| Contraseñas sin hash | PBKDF2 (`HasherPbkdf2`) con comparación en tiempo constante (`FixedTimeEquals`) | Ausente |
| Doble cobro o doble descuento de stock por reintento | P07, P36 y P37 | Ausente |
