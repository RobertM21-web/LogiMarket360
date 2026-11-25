using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Comandos;
using LogiMarket.Application.Comprobantes;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Demostracion;
using LogiMarket.Application.Devoluciones;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Pagos;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Pedidos.Validacion;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Promociones;
using LogiMarket.Infrastructure.Documentos;
using LogiMarket.Infrastructure.Notificaciones;
using LogiMarket.Infrastructure.Pagos;
using LogiMarket.Infrastructure.Pagos.Legado;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Infrastructure;

/// <summary>
/// Registro de dependencias (lo invoca el composition root de WinForms y las pruebas de integración).
/// Singleton: estado de proceso (sesión, métricas, proveedores simulados). Scoped: una operación = un DbContext.
/// </summary>
public static class ContenedorLogiMarket
{
    public static IServiceCollection AgregarLogiMarket(this IServiceCollection services, OpcionesLogiMarket opciones)
    {
        // Configuración y estado del proceso
        services.AddSingleton(opciones);
        services.AddSingleton<IReloj, RelojSistema>();
        services.AddSingleton<ISesionActual, SesionLocal>();
        services.AddSingleton<MetricasSesion>();
        services.AddSingleton<InvocadorComandos>();
        services.AddSingleton<ConfiguracionDemostracion>();
        services.AddSingleton<IModoDemostracion>(sp => sp.GetRequiredService<ConfiguracionDemostracion>());
        services.AddSingleton<IHasherContrasenas, HasherPbkdf2>();
        services.AddSingleton<CatalogoVisualFlyweightFactory>();

        // Políticas de dominio (Strategy + Abstract Factory)
        services.AddSingleton<IPoliticaDescuento, PoliticaMayorista15>();
        services.AddSingleton<IPoliticaDescuento, PoliticaVip10>();
        services.AddSingleton<IPoliticaDescuento, PoliticaSinDescuento>();
        services.AddSingleton<SelectorPoliticaDescuento>();
        services.AddSingleton<CalculadoraPedido>();
        services.AddSingleton<IFabricaEntrega, FabricaEntregaEstandar>();
        services.AddSingleton<IFabricaEntrega, FabricaEntregaExpres>();
        services.AddSingleton<CatalogoFamiliasEntrega>();

        // Persistencia
        services.AddDbContextFactory<LogiMarketDbContext>(o => OpcionesLogiMarket.Configurar(o, opciones.CadenaConexion));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<LogiMarketDbContext>>().CreateDbContext());
        services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajoEf>();
        services.AddScoped<IRepositorioProductos, RepositorioProductosEf>();
        services.AddScoped<IRepositorioKits, RepositorioKitsEf>();
        services.AddScoped<IRepositorioClientes, RepositorioClientesEf>();
        services.AddScoped<IRepositorioPedidos, RepositorioPedidosEf>();
        services.AddScoped<IRepositorioUsuarios, RepositorioUsuariosEf>();
        services.AddScoped<IRepositorioMovimientos, RepositorioMovimientosEf>();
        services.AddScoped<IRepositorioOutbox, RepositorioOutboxEf>();
        services.AddScoped<IRepositorioAuditoria, RepositorioAuditoriaEf>();
        services.AddScoped<IRepositorioNotificaciones, RepositorioNotificacionesEf>();
        services.AddScoped<ISecuencias, SecuenciasEf>();
        services.AddScoped<LecturasEf>();
        services.AddScoped<ILecturaPedidos>(sp => sp.GetRequiredService<LecturasEf>());
        services.AddScoped<ILecturaCatalogo>(sp => sp.GetRequiredService<LecturasEf>());
        services.AddScoped<ILecturaBitacora>(sp => sp.GetRequiredService<LecturasEf>());
        services.AddSingleton<AuditoriaIndependiente>();
        services.AddSingleton<IRegistroAuditoria>(sp => sp.GetRequiredService<AuditoriaIndependiente>());
        services.AddSingleton<IAuditoriaSeguridad>(sp => sp.GetRequiredService<AuditoriaIndependiente>());
        services.AddScoped<SemillaDatos>();
        services.AddScoped<InicializadorBaseDatos>();
        services.AddScoped<IRestablecimientoDatos>(sp => sp.GetRequiredService<InicializadorBaseDatos>());

        // Pagos: Proxy → Decorator → Enrutador → Adapter/V2
        services.AddSingleton<PasarelaLegadaSimulada>();
        services.AddSingleton<IProveedorPago, PasarelaLegadaAdapter>();
        services.AddSingleton<IProveedorPago, PasarelaV2Simulada>();
        services.AddSingleton<ISeleccionProveedorPago>(sp => new SeleccionProveedorPago(sp.GetServices<IProveedorPago>().Select(p => p.Nombre)));
        services.AddSingleton<EnrutadorPasarelas>();
        services.AddSingleton<IAnulacionCobro>(sp => sp.GetRequiredService<EnrutadorPasarelas>());
        services.AddSingleton<IServicioReembolso, ServicioReembolsoSimulado>();
        services.AddScoped<IPasarelaPago>(sp => new PagoAutorizadoProxy(
            new AuditoriaPagoDecorator(sp.GetRequiredService<EnrutadorPasarelas>(), sp.GetRequiredService<IRegistroAuditoria>(), sp.GetRequiredService<ISesionActual>()),
            sp.GetRequiredService<IAutorizador>(),
            sp.GetRequiredService<MetricasSesion>()));

        // Notificaciones (Factory Method) y salidas de documento (Bridge)
        services.AddSingleton<CreadorNotificador, CreadorNotificacionEmail>();
        services.AddSingleton<CreadorNotificador, CreadorNotificacionSms>();
        services.AddSingleton<SalidaVistaPrevia>();
        services.AddSingleton<ISalidaDocumento>(sp => sp.GetRequiredService<SalidaVistaPrevia>());
        services.AddSingleton<ISalidaDocumento>(sp => new SalidaArchivoTxt(sp.GetRequiredService<OpcionesLogiMarket>().CarpetaComprobantes));

        // Aplicación (casos de uso por operación)
        services.AddScoped<IAutorizador, Autorizador>();
        services.AddScoped<ServicioAutenticacion>();
        services.AddScoped<CadenaValidacionPedido>();
        services.AddScoped<ConfirmacionPedidoFacade>();
        services.AddScoped<ServicioPedidos>();
        services.AddScoped<ServicioLogistica>();
        services.AddScoped<ServicioCatalogo>();
        services.AddScoped<ServicioClientes>();
        services.AddScoped<ServicioInventario>();
        services.AddScoped<ServicioComprobantes>();
        services.AddScoped<ServicioDemostracion>();
        services.AddScoped<ConsultasOperativas>();
        services.AddScoped<ConsultaCatalogoVisual>();
        services.AddScoped<DependenciasDevolucion>();
        services.AddScoped<ProcesarDevolucion, DevolucionDefecto>();
        services.AddScoped<ProcesarDevolucion, DevolucionArrepentimiento>();
        services.AddScoped<ServicioDevoluciones>();
        services.AddScoped<IObservadorEventos, NotificacionClienteObserver>();
        services.AddScoped<IObservadorEventos, AuditoriaNegocioObserver>();
        services.AddScoped<IObservadorEventos, MetricasObserver>();
        services.AddScoped<PublicadorEventos>();
        services.AddScoped<ProcesadorOutbox>();
        services.AddScoped<FabricaComandos>();
        return services;
    }
}
