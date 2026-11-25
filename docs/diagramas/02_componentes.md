# Diagrama de componentes (Clean Architecture)

Las dependencias apuntan hacia el dominio. WinForms es el *composition root*: es el único proyecto que conoce las
implementaciones concretas. Se verifica en `ReglasDeDependenciaTests` (`.csproj` y ensamblados compilados).

```mermaid
flowchart TB
    direction TB
    subgraph UI["LogiMarket.WinForms (LogiMarket360.exe)"]
        PR["Program · ConfigurarServicios<br/>(composition root DI)"]
        FP["FormularioPrincipal + 11 pantallas<br/>PantallaBase : IColegaPantalla"]
        MED["FormularioPrincipalMediator"]
        EJ["EjecutorOperaciones<br/>(ámbito DI por operación)"]
    end
    subgraph INF["LogiMarket.Infrastructure"]
        EF["LogiMarketDbContext · Migraciones · SemillaDatos<br/>UnidadDeTrabajo · Repositorios · Lecturas"]
        PAY["AuditoriaPagoDecorator · EnrutadorPasarelas<br/>PasarelaLegadaAdapter · PasarelaV2Simulada"]
        NOT["CreadorNotificacionEmail / Sms"]
        DOC["SalidaVistaPrevia · SalidaArchivoTxt"]
        SEG["HasherPbkdf2 · AuditoriaIndependiente"]
    end
    subgraph APP["LogiMarket.Application"]
        CU["ServicioPedidos · ConfirmacionPedidoFacade<br/>ServicioLogistica · ServicioDevoluciones<br/>ServicioCatalogo · ServicioInventario"]
        CMD["Comandos + InvocadorComandos"]
        CH["CadenaValidacionPedido (Chain)"]
        PX["PagoAutorizadoProxy · Autorizador"]
        OBS["PublicadorEventos · ProcesadorOutbox · Observadores"]
        Q["ConsultasOperativas · InterpreteFiltro"]
        PU["Puertos: IRepositorio* · IUnidadDeTrabajo · IPasarelaPago<br/>CreadorNotificador · ISalidaDocumento · IReloj"]
        MS["MetricasSesion (Singleton)"]
    end
    subgraph DOM["LogiMarket.Domain"]
        AG["Pedido + EstadoPedido (State) · PedidoBuilder<br/>PedidoBorrador (Memento / Prototype)"]
        CAT["IElementoCatalogo (Composite) · Iterator · Visitor"]
        PRO["IPoliticaDescuento (Strategy) · CalculadoraPedido"]
        ENT["IFabricaEntrega (Abstract Factory)"]
    end
    DB[("SQLite<br/>logimarket.db")]

    UI --> APP
    UI --> INF
    UI --> DOM
    INF --> APP
    INF --> DOM
    APP --> DOM
    EF --> DB
```

Los adaptadores de Infrastructure (persistencia, pagos, notificadores, salidas) **implementan** los puertos declarados en
Application (`IRepositorio*`, `IUnidadDeTrabajo`, `IProveedorPago`, `CreadorNotificador`, `ISalidaDocumento`): inversión de dependencias.

| Proyecto | Referencia a | Prohibido (verificado por prueba) |
|---|---|---|
| Domain | — | Cualquier proyecto o paquete (EF, WinForms, Application) |
| Application | Domain | EF Core, SQLite, WinForms, Infrastructure |
| Infrastructure | Application, Domain | WinForms |
| WinForms | Application, Infrastructure, Domain | — (composition root) |
| Tests | Todos | — |
