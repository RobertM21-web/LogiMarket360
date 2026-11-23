using LogiMarket.Application.Consultas.Filtro;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Comun;

/// <summary>
/// Ejecuta cada operación de la interfaz en su propio ámbito DI (un DbContext por operación) y traduce
/// las excepciones esperadas a mensajes claros. La autorización real la decide Application.
/// </summary>
public sealed class EjecutorOperaciones
{
    private readonly IServiceProvider _raiz;

    public EjecutorOperaciones(IServiceProvider raiz)
    {
        _raiz = raiz;
    }

    /// <summary>Servicio de proceso (singleton): sesión, métricas, invocador, salidas…</summary>
    public T Global<T>()
        where T : notnull => _raiz.GetRequiredService<T>();

    public async Task<(bool Ok, T? Valor)> IntentarAsync<T>(Control dueno, Func<IServiceProvider, Task<T>> accion)
    {
        var cursor = Cursor.Current;
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            await using var ambito = _raiz.CreateAsyncScope();
            return (true, await accion(ambito.ServiceProvider));
        }
        catch (Exception ex)
        {
            Mostrar(dueno, ex);
            return (false, default);
        }
        finally
        {
            Cursor.Current = cursor;
        }
    }

    public async Task<bool> IntentarAsync(Control dueno, Func<IServiceProvider, Task> accion) =>
        (await IntentarAsync(dueno, async sp =>
        {
            await accion(sp);
            return true;
        })).Ok;

    /// <summary>Muestra el resultado de una operación de negocio (éxito o motivo del rechazo).</summary>
    public static bool Informar(Control dueno, ResultadoOperacion r, string titulo)
    {
        if (r.Exito)
        {
            MessageBox.Show(dueno, r.Mensaje, titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(dueno, $"{r.Mensaje}\n\nCódigo: {r.Codigo}", titulo, MessageBoxButtons.OK, r.Codigo == "ACCESO_DENEGADO" ? MessageBoxIcon.Stop : MessageBoxIcon.Warning);
        }

        return r.Exito;
    }

    public static bool Confirmar(Control dueno, string mensaje, string titulo) =>
        MessageBox.Show(dueno, mensaje, titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private static void Mostrar(Control dueno, Exception ex)
    {
        var (titulo, mensaje, icono) = ex switch
        {
            AccesoDenegadoException a => ("Acceso denegado", a.Message, MessageBoxIcon.Stop),
            ReglaNegocioException r => ("Regla de negocio", $"{r.Message}\n\nCódigo: {r.Codigo}", MessageBoxIcon.Warning),
            FiltroInvalidoException f => ("Filtro inválido", f.Message, MessageBoxIcon.Warning),
            ConflictoPersistenciaException c => ("Conflicto de datos", c.Message + "\n\nActualice la vista e intente de nuevo.", MessageBoxIcon.Warning),
            ArgumentException a => ("Dato inválido", a.Message, MessageBoxIcon.Warning),
            _ => ("Error inesperado", $"No se pudo completar la operación.\n\n{ex.GetType().Name}: {ex.Message}", MessageBoxIcon.Error),
        };
        if (icono == MessageBoxIcon.Error)
        {
            RegistroErrores.Escribir(ex);
        }

        MessageBox.Show(dueno, mensaje, titulo, MessageBoxButtons.OK, icono);
    }
}

/// <summary>Registro local de errores inesperados (no expone datos sensibles).</summary>
public static class RegistroErrores
{
    public static string Ruta { get; set; } = Path.Combine(Path.GetTempPath(), "LogiMarket360_errores.log");

    public static void Escribir(Exception ex)
    {
        try
        {
            File.AppendAllText(Ruta, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch (IOException)
        {
            // El registro de errores nunca debe derribar la aplicación.
        }
    }
}
