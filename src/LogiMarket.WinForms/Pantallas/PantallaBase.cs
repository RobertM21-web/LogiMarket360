using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Mediador;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>
/// UserControl base de cada pantalla: título, recarga diferida (solo si está visible) y acceso al mediador.
/// Los permisos del rol solo habilitan/deshabilitan controles; Application vuelve a verificarlos.
/// </summary>
public class PantallaBase : UserControl, IColegaPantalla
{
    private readonly Label _titulo;
    private readonly Label _subtitulo;
    private bool _desactualizada = true;
    private bool _cargando;

    protected PantallaBase()
    {
        // Constructor para el diseñador.
        Ejecutor = null!;
        Mediador = null!;
        _titulo = new Label();
        _subtitulo = new Label();
    }

    protected PantallaBase(string clave, string titulo, string subtitulo, EjecutorOperaciones ejecutor, IMediadorPantallas mediador)
    {
        Clave = clave;
        Ejecutor = ejecutor;
        Mediador = mediador;
        Dock = DockStyle.Fill;
        BackColor = Tema.Fondo;
        Font = Tema.Base;
        Padding = new Padding(16, 12, 16, 12);

        _titulo = new Label { Text = titulo, Font = Tema.Titulo, ForeColor = Tema.Texto, AutoSize = true, Dock = DockStyle.Top };
        _subtitulo = new Label { Text = subtitulo, ForeColor = Tema.TextoSuave, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 2, 0, 10) };
        Contenido = new Panel { Dock = DockStyle.Fill };
        Controls.Add(Contenido);
        Controls.Add(_subtitulo);
        Controls.Add(_titulo);
        mediador.Registrar(this);
    }

    public string Clave { get; } = string.Empty;

    public string Titulo => _titulo.Text;

    protected Panel Contenido { get; } = new();

    protected EjecutorOperaciones Ejecutor { get; }

    protected IMediadorPantallas Mediador { get; }

    protected UsuarioSesion? Usuario => Ejecutor.Global<ISesionActual>().Usuario;

    protected bool Puede(Operacion operacion) => Usuario is { } u && PoliticaPermisos.Permite(u.Rol, operacion);

    public async Task InvalidarAsync()
    {
        _desactualizada = true;
        if (Visible && Parent is not null)
        {
            await RecargarSiCorrespondeAsync();
        }
    }

    public virtual void Recibir(EventoPantalla evento, object? dato)
    {
    }

    /// <summary>Recarga forzada (botón Actualizar / F5).</summary>
    public Task ActualizarAsync()
    {
        _desactualizada = true;
        return RecargarSiCorrespondeAsync();
    }

    protected Task NotificarAsync(EventoPantalla evento, object? dato = null) => Mediador.NotificarAsync(this, evento, dato);

    /// <summary>Carga los datos de la vista desde Application (cada pantalla la implementa).</summary>
    protected virtual Task CargarAsync() => Task.CompletedTask;

    /// <summary>Aplica habilitación de controles según el rol de la sesión.</summary>
    protected virtual void AplicarPermisos()
    {
    }

    protected override async void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            AplicarPermisos();
            await RecargarSiCorrespondeAsync();
        }
    }

    private async Task RecargarSiCorrespondeAsync()
    {
        if (!_desactualizada || _cargando)
        {
            return;
        }

        _cargando = true;
        try
        {
            // Si otra invalidación llega durante la carga, se vuelve a cargar al terminar (no se pierde).
            while (_desactualizada)
            {
                _desactualizada = false;
                await CargarAsync();
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Escribir(ex);
            MessageBox.Show(this, $"No se pudo cargar {Titulo}: {ex.Message}", "LogiMarket 360", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cargando = false;
        }
    }
}
