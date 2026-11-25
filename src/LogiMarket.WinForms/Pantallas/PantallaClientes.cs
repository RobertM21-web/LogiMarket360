using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Clientes;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Componentes;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>Pantalla 04 — Clientes: búsqueda, alta y ficha editable.</summary>
public sealed class PantallaClientes : PantallaBase
{
    private readonly TextBox _buscar = new() { PlaceholderText = "Nombre o identificación", Width = 260, AccessibleName = "Buscar cliente" };
    private readonly GrillaDatos _grilla = new() { AccessibleName = "Clientes" };
    private readonly Label _codigo = Tema.Valor(negrita: true);
    private readonly TextBox _nombre = new();
    private readonly TextBox _identificacion = new();
    private readonly ComboBox _categoria = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _email = new();
    private readonly TextBox _telefono = new();
    private readonly ComboBox _canal = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _activo = new() { Text = "Cliente activo", AutoSize = true };
    private readonly ErrorProvider _errores = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private readonly Button _nuevo;
    private readonly Button _guardar;
    private readonly Button _cancelar;
    private List<ClienteDto> _clientes = [];
    private ClienteDto? _edicion;
    private bool _creando;

    public PantallaClientes(EjecutorOperaciones ejecutor, IMediadorPantallas mediador)
        : base(ClavesPantalla.Clientes, "Clientes", "La categoría (NORMAL, VIP, MAYORISTA) determina la política de descuento; el canal define el aviso.", ejecutor, mediador)
    {
        _categoria.Items.AddRange(Enum.GetNames<CategoriaCliente>());
        _canal.Items.AddRange(Enum.GetNames<CanalNotificacion>());
        _grilla.Columna("Código", nameof(ClienteDto.Codigo), peso: 50)
            .Columna("Nombre", nameof(ClienteDto.Nombre), peso: 120)
            .Columna("Identificación", nameof(ClienteDto.Identificacion), peso: 80)
            .Columna("Categoría", nameof(ClienteDto.Categoria), peso: 60)
            .Columna("Contacto", nameof(ClienteDto.Email), peso: 130)
            .Columna("Canal", nameof(ClienteDto.Canal), peso: 45);
        _grilla.ClaveDe = o => ((ClienteDto)o).Id;
        _grilla.SelectionChanged += (_, _) =>
        {
            if (!_creando)
            {
                Editar(_grilla.Seleccionado<ClienteDto>());
            }
        };
        _buscar.TextChanged += (_, _) => Filtrar();

        _nuevo = Tema.Boton("&Nuevo cliente", (_, _) => Nuevo(), primario: true);
        _guardar = Tema.Boton("&Guardar cambios", async (_, _) => await GuardarAsync(), primario: true);
        _cancelar = Tema.Boton("Ca&ncelar", (_, _) =>
        {
            _creando = false;
            Editar(_grilla.Seleccionado<ClienteDto>());
        });

        var ficha = new Panel { Dock = DockStyle.Fill };
        ficha.Controls.Add(Tema.Fila(_guardar, _cancelar));
        ficha.Controls.Add(_activo);
        ficha.Controls.Add(Tema.Ficha(("Código", _codigo), ("Nombre", _nombre), ("Identificación", _identificacion), ("Categoría", _categoria), ("Correo", _email), ("Teléfono", _telefono), ("Canal de aviso", _canal)));
        _activo.Dock = DockStyle.Top;

        var division = new SplitContainer { Dock = DockStyle.Fill };
        division.Panel1.Controls.Add(Tema.Tarjeta("Clientes", _grilla));
        division.Panel2.Controls.Add(Tema.Tarjeta("Ficha del cliente", ficha));
        Contenido.Controls.Add(division);
        Contenido.Controls.Add(Tema.Fila(Tema.Etiqueta("&Buscar:"), _buscar, _nuevo));
        Resize += (_, _) => division.SplitterDistance = Math.Max(200, (int)(division.Width * 0.6));
    }

    protected override void AplicarPermisos()
    {
        var puede = Puede(Operacion.GestionarClientes);
        _nuevo.Enabled = _guardar.Enabled = puede;
        foreach (var c in new Control[] { _nombre, _identificacion, _categoria, _email, _telefono, _canal, _activo })
        {
            c.Enabled = puede;
        }
    }

    protected override async Task CargarAsync()
    {
        var (ok, clientes) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ConsultasOperativas>().ClientesAsync());
        if (ok)
        {
            _clientes = clientes!.ToList();
            Filtrar();
        }
    }

    private void Filtrar()
    {
        var t = _buscar.Text.Trim();
        _grilla.Mostrar(_clientes.Where(c => t.Length == 0
            || c.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase)
            || c.Identificacion.Contains(t, StringComparison.OrdinalIgnoreCase)
            || c.Codigo.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    private void Editar(ClienteDto? c)
    {
        _errores.Clear();
        _edicion = c;
        _codigo.Text = c?.Codigo ?? "—";
        _nombre.Text = c?.Nombre ?? string.Empty;
        _identificacion.Text = c?.Identificacion ?? string.Empty;
        _categoria.SelectedItem = c is null ? null : Enum.GetNames<CategoriaCliente>().First(n => n.Equals(c.Categoria, StringComparison.OrdinalIgnoreCase));
        _email.Text = c?.Email ?? string.Empty;
        _telefono.Text = c?.Telefono ?? string.Empty;
        _canal.SelectedItem = c is null ? null : Enum.GetNames<CanalNotificacion>().First(n => n.Equals(c.Canal, StringComparison.OrdinalIgnoreCase));
        _activo.Checked = c?.Activo ?? true;
    }

    private void Nuevo()
    {
        _creando = true;
        Editar(null);
        _codigo.Text = "(se asigna al guardar)";
        _categoria.SelectedIndex = 0;
        _canal.SelectedIndex = 0;
        _nombre.Focus();
    }

    private async Task GuardarAsync()
    {
        _errores.Clear();
        if (string.IsNullOrWhiteSpace(_nombre.Text))
        {
            _errores.SetError(_nombre, "El nombre es obligatorio.");
            _nombre.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_identificacion.Text))
        {
            _errores.SetError(_identificacion, "La identificación es obligatoria.");
            _identificacion.Focus();
            return;
        }

        if (_categoria.SelectedItem is null || _canal.SelectedItem is null)
        {
            _errores.SetError(_categoria, "Seleccione categoría y canal.");
            return;
        }

        if (!_creando && _edicion is null)
        {
            return;
        }

        var datos = new DatosCliente(
            _nombre.Text.Trim(),
            _identificacion.Text.Trim(),
            Enum.Parse<CategoriaCliente>((string)_categoria.SelectedItem),
            _email.Text.Trim(),
            _telefono.Text.Trim(),
            Enum.Parse<CanalNotificacion>((string)_canal.SelectedItem));
        var creando = _creando;
        var id = _edicion?.Id ?? 0;
        var (ok, r) = await Ejecutor.IntentarAsync(this, sp => creando
            ? sp.GetRequiredService<ServicioClientes>().CrearAsync(datos)
            : sp.GetRequiredService<ServicioClientes>().ActualizarAsync(id, datos, _activo.Checked));
        if (ok && EjecutorOperaciones.Informar(this, r!, "Clientes"))
        {
            _creando = false;
            await NotificarAsync(EventoPantalla.ClientesActualizados);
        }
    }
}
