using LogiMarket.Application.Seguridad;
using LogiMarket.Infrastructure;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Formularios;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms;

/// <summary>
/// Composition root: único lugar donde se conocen las implementaciones concretas. Construye el contenedor
/// DI, crea/migra la base y abre el flujo Acceso → Ventana principal.
/// Argumentos opcionales: <c>--datos &lt;carpeta&gt;</c> (ubicación alternativa de la BD) y <c>--restablecer</c>.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, e) => MostrarErrorFatal(e.Exception, terminar: false);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => MostrarErrorFatal((Exception)e.ExceptionObject, terminar: true);

        var opciones = CrearOpciones(args);
        RegistroErrores.Ruta = Path.Combine(Path.GetDirectoryName(opciones.RutaBaseDatos)!, "errores.log");
        using var servicios = ConfigurarServicios(opciones);

        try
        {
            // Inicialización fuera del hilo de UI para no bloquear con E/S de SQLite.
            Task.Run(async () =>
            {
                await using var ambito = servicios.CreateAsyncScope();
                var inicializador = ambito.ServiceProvider.GetRequiredService<InicializadorBaseDatos>();
                if (args.Contains("--restablecer"))
                {
                    await inicializador.RestablecerAsync();
                }
                else
                {
                    await inicializador.InicializarAsync();
                }
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            RegistroErrores.Escribir(ex);
            MessageBox.Show($"No se pudo preparar la base de datos en:\n{opciones.RutaBaseDatos}\n\n{ex.Message}", "LogiMarket 360", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        while (true)
        {
            using (var acceso = servicios.GetRequiredService<FormularioAcceso>())
            {
                if (acceso.ShowDialog() != DialogResult.OK)
                {
                    return 0;
                }
            }

            bool cerroSesion;
            using (var ambitoUi = servicios.CreateScope())
            using (var principal = ambitoUi.ServiceProvider.GetRequiredService<FormularioPrincipal>())
            {
                System.Windows.Forms.Application.Run(principal);
                cerroSesion = principal.CerroSesion;
            }

            servicios.GetRequiredService<ISesionActual>().Cerrar();
            if (!cerroSesion)
            {
                return 0;
            }
        }
    }

    public static ServiceProvider ConfigurarServicios(OpcionesLogiMarket opciones)
    {
        var servicios = new ServiceCollection().AgregarLogiMarket(opciones);
        servicios.AddSingleton<EjecutorOperaciones>();
        servicios.AddTransient<FormularioAcceso>();

        // Un mediador y un formulario principal por sesión de usuario (ámbito de UI).
        servicios.AddScoped<FormularioPrincipalMediator>();
        servicios.AddScoped<IMediadorPantallas>(sp => sp.GetRequiredService<FormularioPrincipalMediator>());
        servicios.AddScoped<FormularioPrincipal>();
        return servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static OpcionesLogiMarket CrearOpciones(string[] args)
    {
        var i = Array.IndexOf(args, "--datos");
        if (i < 0 || i + 1 >= args.Length)
        {
            return new OpcionesLogiMarket();
        }

        var carpeta = Path.GetFullPath(args[i + 1]);
        return new OpcionesLogiMarket
        {
            RutaBaseDatos = Path.Combine(carpeta, "logimarket.db"),
            CarpetaComprobantes = Path.Combine(carpeta, "comprobantes"),
        };
    }

    private static void MostrarErrorFatal(Exception ex, bool terminar)
    {
        RegistroErrores.Escribir(ex);
        MessageBox.Show(
            $"Se produjo un error inesperado{(terminar ? " y la aplicación se cerrará" : string.Empty)}.\n\n{ex.Message}\n\nDetalle en: {RegistroErrores.Ruta}",
            "LogiMarket 360",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
