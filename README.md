# LogiMarket 360

Aplicativo de escritorio **Windows Forms (.NET 8)** para la gestión comercial y logística de una tienda: catálogo con kits
anidados, clientes, carrito con deshacer/rehacer, pedidos, cobro simulado (proveedores Legacy y V2), despacho con guía,
devoluciones con reembolso, inventario, reportes con comprobantes, auditoría y modo de demostración.

Construido con **Clean Architecture** (5 proyectos), **SOLID** y los **23 patrones GoF**, cada uno con colaboración real y
una prueba automatizada (`docs/MATRIZ_PATRONES.md`).

| Indicador | Valor |
|---|---|
| Pruebas automatizadas | **98 / 98 aprobadas** (`docs/evidencias/pruebas/resultado_pruebas.txt`) |
| Matriz P01–P38 | **38 / 38** (`docs/MATRIZ_PRUEBAS_38.md`) |
| Patrones GoF | **23 / 23** (`docs/MATRIZ_PATRONES.md`) |
| Riesgos refactorizados con prueba antes/después | **10** (`docs/RIESGOS_REFACTORIZACION.md`) |

## 1. Requisitos

| Componente | Versión |
|---|---|
| Sistema operativo | Windows 10 (1809+) u 11, x64 |
| SDK | **.NET 8** (probado con 8.0.428) — <https://dotnet.microsoft.com/download/dotnet/8.0> |
| IDE (opcional) | Visual Studio 2022 17.8+ con la carga «Desarrollo de escritorio de .NET», o VS Code + C# Dev Kit |
| Base de datos | SQLite embebida (paquete NuGet, no requiere instalación) |

Paquetes NuGet (se restauran solos): `Microsoft.EntityFrameworkCore.Sqlite` 8.0.11, `Microsoft.EntityFrameworkCore.Design`
8.0.11, `Microsoft.Extensions.DependencyInjection` 8.0.1; pruebas: xUnit 2.5.3, FluentAssertions 6.12.2.

Compruebe el SDK:

```powershell
dotnet --list-sdks   # debe listar 8.0.x
```

## 2. Restaurar, compilar y probar

Desde la carpeta que contiene `LogiMarket360.sln`:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release            # 98 pruebas: dominio, aplicación, integración SQLite, presentación y arquitectura
```

Las pruebas de integración crean bases SQLite **temporales y aisladas** en `%TEMP%\LogiMarketPruebas\…`. No tocan la base de la
aplicación.

## 3. Base de datos: migraciones y datos semilla

No hace falta ejecutar comandos de migración. Al iniciar, `InicializadorBaseDatos`:

1. crea la carpeta de datos si no existe;
2. aplica las migraciones de EF Core (`Persistencia/Migraciones/20261122150500_Inicial`) sobre un archivo inexistente o desactualizado;
3. activa el modo WAL de SQLite;
4. carga los **datos semilla idempotentes** (no se duplican al relanzar): 5 productos (P01–P05), 2 kits (KIT_OFICINA, KIT_TELETRABAJO), 4 clientes y 4 usuarios con contraseña en hash PBKDF2.

Ubicación predeterminada: `%LOCALAPPDATA%\LogiMarket360\logimarket.db`. Los comprobantes TXT exportados se guardan en
`%LOCALAPPDATA%\LogiMarket360\comprobantes\`.

La fábrica de diseño `FabricaDisenoDbContext` solo se usa para generar migraciones nuevas con `dotnet ef migrations add`;
la aplicación nunca requiere ejecutar `dotnet ef database update`.

## 4. Ejecutar la aplicación

```powershell
dotnet run -c Release --project src/LogiMarket.WinForms
```

o directamente `src\LogiMarket.WinForms\bin\Release\net8.0-windows\LogiMarket360.exe`.

Argumentos opcionales (también en `scripts\ejecutar-demo.ps1`):

| Argumento | Efecto |
|---|---|
| `--datos <carpeta>` | Usa una carpeta de datos alternativa (`<carpeta>\logimarket.db`, `<carpeta>\comprobantes`). Útil para demostraciones aisladas. |
| `--restablecer` | Elimina y recrea la base de datos de **esa** carpeta con los datos semilla (ver §6). |

## 5. Cuentas de demostración

Credenciales **ficticias**, solo para evaluación. Se guardan con hash y sal; ninguna contraseña está en texto plano en la base.

| Usuario | Contraseña | Rol | Puede |
|---|---|---|---|
| `admin.demo` | `Admin#2026` | ADMIN | Todo, incluido el modo de demostración y el restablecimiento de datos |
| `ventas.demo` | `Ventas#2026` | VENTAS | Clientes, carrito, validar, cobrar, cancelar, devoluciones |
| `bodega.demo` | `Bodega#2026` | BODEGA | Despachar y registrar entregas |
| `consulta.demo` | `Consulta#2026` | CONSULTA | Solo lectura (un intento de cobro se deniega y se audita) |

Clientes semilla: **C001 Ana López** (VIP, aviso por EMAIL), **C002 Luis Ruiz** (NORMAL, SMS), **C003 Sofía Díaz** (NORMAL,
EMAIL), **C004 Carmen Vega** (NORMAL, EMAIL; recibe MAYORISTA por volumen).

### Recorrido sugerido (casos oficiales)

1. `ventas.demo` → **Carrito**: Ana + KIT_TELETRABAJO ×1, Estándar → total **USD 59.00** → *Validar pedido* → *Confirmar cobro*.
2. Luis + P04 ×2 + P05 ×2, Exprés → **USD 102.00**. Carmen + P03 ×10, Estándar → **USD 90.00**.
3. Ana + KIT_TELETRABAJO ×6 → rechazo «Stock insuficiente de P02».
4. `bodega.demo` → **Despacho**: marcar como despachado (guía `GUIA-2026-0001`) y registrar la entrega.
5. `ventas.demo` → **Devoluciones**: PED-001, motivo Defecto → reembolso **USD 54.00** (sin envío).
6. `admin.demo` → **Auditoría / pruebas**: inducir un rechazo de cobro o una caída del Outbox y reprocesar.

Atajos: `Ctrl+1` … `Ctrl+9` navegan por el menú lateral y `F5` actualiza la pantalla.

## 6. Restablecer los datos de prueba de forma segura

Hay tres opciones. Ninguna afecta a otras carpetas ni a la base de las pruebas automatizadas.

- **Desde la aplicación (ADMIN):** *Auditoría / pruebas* → **Restablecer datos de prueba**. Pide confirmación, recrea la base con la semilla, reinicia los interruptores del modo de demostración y las métricas de sesión, y deja constancia en auditoría (`DATOS RESTABLECIDOS`).
- **Al iniciar:** `LogiMarket360.exe --restablecer` (o `--datos <carpeta> --restablecer` para una carpeta concreta).
- **Script:** `scripts\restablecer-datos.ps1 [-Datos <carpeta>]`, que cierra la aplicación si está abierta y la relanza con `--restablecer`.

El restablecimiento borra y recrea solo la base SQLite de la carpeta de datos elegida. Los comprobantes TXT ya exportados se
conservan en `comprobantes\` y pueden borrarse a mano. Ninguna opción usa datos personales reales.

## 7. Estructura de la solución

```
LogiMarket360.sln
├─ src/
│  ├─ LogiMarket.Domain/          Entidades e invariantes, State, Composite, Iterator, Visitor, Strategy, Builder,
│  │                              Prototype, Memento, Abstract Factory (familias de entrega). Sin dependencias.
│  ├─ LogiMarket.Application/     Casos de uso, puertos, Facade, Proxy, Chain, Command, Interpreter, Observer + Outbox,
│  │                              Template Method, Bridge (abstracción), Flyweight, Singleton, CQRS de lectura.
│  ├─ LogiMarket.Infrastructure/  EF Core + SQLite, migraciones, semilla, UoW, repositorios, Adapter, Decorator,
│  │                              Factory Method (notificadores), salidas Bridge, hash PBKDF2, registro DI.
│  └─ LogiMarket.WinForms/        Composition root, acceso, ventana principal, 11 pantallas, Mediator.
├─ tests/LogiMarket.Tests/        Dominio, Aplicación, Integración (SQLite temporal), Presentación, Arquitectura, Calidad.
├─ scripts/                       ejecutar-demo.ps1 · restablecer-datos.ps1 · empaquetar.ps1
└─ docs/                          Requisitos, decisiones, diagramas, matrices, riesgos y evidencias.
```

Reglas de dependencia (verificadas por `ReglasDeDependenciaTests`): Domain no referencia nada; Application solo a Domain;
Infrastructure a Application y Domain; WinForms es el único que conoce las implementaciones concretas.

## 8. Documentación

| Documento | Contenido |
|---|---|
| `docs/REQUISITOS.md` | Actores, casos de uso, RF y RNF medibles |
| `docs/DECISIONES.md` | Decisiones de diseño y supuestos |
| `docs/diagramas/` | Contexto, componentes, clases críticas, estados, secuencias y modelo relacional (Mermaid + SVG) |
| `docs/MATRIZ_PATRONES.md` | 23/23 patrones: problema, participantes, archivo/método, recorrido, prueba, evidencia |
| `docs/MATRIZ_PRUEBAS_38.md` | P01–P38 con esperado, obtenido, evidencia, fecha y método |
| `docs/RIESGOS_REFACTORIZACION.md` | 10 riesgos reales con commit y prueba antes/después |
| `docs/evidencias/` | Capturas reales en Windows, logs de pruebas, salidas antes/después, script de automatización |

## 9. Empaquetado

```powershell
scripts\empaquetar.ps1 -Apellidos "MONTESDEOCA_CHAFLA" -Nombres "ROBERT_ALEXANDER"
```

El script genera `APE04_LogiMarket360_<APELLIDOS>_<NOMBRES>.zip` a partir de `git archive` (solo archivos versionados).
Excluye `bin/`, `obj/`, `.vs/`, bases de datos, la guía de referencia y cualquier credencial real. Luego descomprime el ZIP en
una carpeta temporal, compila y ejecuta las pruebas desde cero para verificarlo.

## 10. Limitaciones conocidas

- Pasarelas de pago, correo y SMS **simulados**: no hay cobros ni envíos reales.
- Aplicación monousuario de escritorio. La concurrencia entre sesiones se protege con concurrencia optimista (`Version` en `Producto`) y con índices únicos, pero no hay sincronización en red.
- Los comprobantes son documentos internos de demostración: no son facturas tributarias ni calculan IVA.
