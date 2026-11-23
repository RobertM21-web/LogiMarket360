using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Comun;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Formularios;

/// <summary>Pantalla 01 — Acceso al sistema con credenciales de demostración.</summary>
public sealed class FormularioAcceso : Form
{
    private readonly EjecutorOperaciones _ejecutor;
    private readonly TextBox _usuario = new() { Width = 260, AccessibleName = "Usuario", PlaceholderText = "p. ej. ventas.demo" };
    private readonly TextBox _contrasena = new() { Width = 260, UseSystemPasswordChar = true, AccessibleName = "Contraseña" };
    private readonly Label _error = new() { AutoSize = true, ForeColor = Tema.Peligro, MaximumSize = new Size(300, 0) };
    private readonly Button _ingresar;

    public FormularioAcceso(EjecutorOperaciones ejecutor)
    {
        _ejecutor = ejecutor;
        Text = "LogiMarket 360 — Acceso";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 520);
        MinimumSize = new Size(560, 480);
        BackColor = Tema.Lateral;
        Font = Tema.Base;
        KeyPreview = true;

        _ingresar = Tema.Boton("&Ingresar", async (_, _) => await IngresarAsync(), primario: true);
        _ingresar.Margin = new Padding(0, 12, 0, 0);
        AcceptButton = _ingresar;

        var tarjeta = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Tema.Superficie,
            Padding = new Padding(28, 22, 28, 22),
            Anchor = AnchorStyles.None,
        };
        tarjeta.Controls.Add(new Label { Text = "LOGIMARKET 360", Font = Tema.Titulo, ForeColor = Tema.Primario, AutoSize = true });
        tarjeta.Controls.Add(new Label { Text = "ACCESO AL SISTEMA", Font = Tema.Subtitulo, AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        tarjeta.Controls.Add(new Label { Text = "Credenciales de demostración y perfil de acceso", ForeColor = Tema.TextoSuave, AutoSize = true, Margin = new Padding(0, 0, 0, 12) });
        tarjeta.Controls.Add(new Label { Text = "&Usuario", AutoSize = true });
        tarjeta.Controls.Add(_usuario);
        tarjeta.Controls.Add(new Label { Text = "&Contraseña", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        tarjeta.Controls.Add(_contrasena);
        tarjeta.Controls.Add(_error);
        tarjeta.Controls.Add(_ingresar);
        tarjeta.Controls.Add(new Label
        {
            Text = "Roles: ADMIN · VENTAS · BODEGA · CONSULTA\nCuentas de prueba: admin.demo, ventas.demo, bodega.demo, consulta.demo\n(contraseñas ficticias documentadas en el README)",
            ForeColor = Tema.TextoSuave,
            AutoSize = true,
            Margin = new Padding(0, 16, 0, 0),
        });

        var centro = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
        centro.Controls.Add(tarjeta);
        Controls.Add(centro);
    }

    public UsuarioSesion? UsuarioAutenticado { get; private set; }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _usuario.Focus();
    }

    private async Task IngresarAsync()
    {
        _error.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(_usuario.Text) || string.IsNullOrEmpty(_contrasena.Text))
        {
            _error.Text = "Ingrese usuario y contraseña.";
            (string.IsNullOrWhiteSpace(_usuario.Text) ? _usuario : _contrasena).Focus();
            return;
        }

        _ingresar.Enabled = false;
        var (ok, resultado) = await _ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioAutenticacion>().IniciarAsync(_usuario.Text.Trim(), _contrasena.Text));
        _ingresar.Enabled = true;
        if (!ok || resultado is null)
        {
            return;
        }

        if (!resultado.Exito)
        {
            _error.Text = resultado.Mensaje;
            _contrasena.SelectAll();
            _contrasena.Focus();
            return;
        }

        UsuarioAutenticado = resultado.Usuario;
        DialogResult = DialogResult.OK;
        Close();
    }
}
