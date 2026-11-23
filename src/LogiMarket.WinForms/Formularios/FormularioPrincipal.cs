using LogiMarket.Application.Metricas;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Mediador;
using LogiMarket.WinForms.Pantallas;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Formularios;

/// <summary>
/// Ventana principal: encabezado, barra lateral de navegación, contenedor de pantallas y barra de métricas.
/// Implementa <see cref="INavegador"/> para el mediador; no invoca métodos de ninguna pantalla en concreto.
/// </summary>
public sealed class FormularioPrincipal : Form, INavegador
{
    private static readonly (string Clave, string Texto, Type Tipo)[] Menu =
    [
        (ClavesPantalla.Panel, "Panel general", typeof(PantallaPanelGeneral)),
        (ClavesPantalla.Catalogo, "Catálogo y kits", typeof(PantallaCatalogo)),
        (ClavesPantalla.Clientes, "Clientes", typeof(PantallaClientes)),
        (ClavesPantalla.Carrito, "Carrito / borrador", typeof(PantallaCarrito)),
        (ClavesPantalla.Pedidos, "Pedidos e historial", typeof(PantallaPedidos)),
        (ClavesPantalla.Cobro, "Cobro", typeof(PantallaCobro)),
        (ClavesPantalla.Despacho, "Despacho", typeof(PantallaDespacho)),
        (ClavesPantalla.Devoluciones, "Devoluciones", typeof(PantallaDevoluciones)),
        (ClavesPantalla.Inventario, "Inventario", typeof(PantallaInventario)),
        (ClavesPantalla.Reportes, "Reportes", typeof(PantallaReportes)),
        (ClavesPantalla.Auditoria, "Auditoría / pruebas", typeof(PantallaAuditoria)),
    ];

    private readonly Dictionary<string, PantallaBase> _pantallas = new();
    private readonly Dictionary<string, Button> _botones = new();
    private readonly Panel _contenedor = new() { Dock = DockStyle.Fill, BackColor = Tema.Fondo };
    private readonly ToolStripStatusLabel _estadoMetricas = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly MetricasSesion _metricas;
    private readonly EjecutorOperaciones _ejecutor;
    private PantallaBase? _actual;

    public FormularioPrincipal(IServiceProvider servicios, FormularioPrincipalMediator mediador, MetricasSesion metricas, ISesionActual sesion, EjecutorOperaciones ejecutor)
    {
        _metricas = metricas;
        _ejecutor = ejecutor;
        var usuario = sesion.Usuario ?? throw new InvalidOperationException("No hay sesión iniciada.");
        Text = $"LogiMarket 360 — {usuario.NombreCompleto}";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1360, 860);
        MinimumSize = new Size(1080, 680);
        Font = Tema.Base;
        BackColor = Tema.Fondo;
        KeyPreview = true;
        mediador.AsignarNavegador(this);

        // Encabezado
        var encabezado = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Tema.Superficie, Padding = new Padding(18, 0, 18, 0) };
        var marca = new Label { Text = "LOGIMARKET 360", Font = Tema.Titulo, ForeColor = Tema.Primario, AutoSize = true, Location = new Point(18, 13) };
        var rol = new Label
        {
            Text = $"«{usuario.Rol.ToString().ToUpperInvariant()}» · Sesión local · {usuario.NombreUsuario}",
            AutoSize = true,
            ForeColor = Tema.TextoSuave,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        var salir = Tema.Boton("Cerrar &sesión", (_, _) => CerrarSesion(), accesible: "Cerrar sesión");
        salir.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        encabezado.Controls.AddRange([marca, rol, salir]);
        encabezado.Resize += (_, _) =>
        {
            salir.Location = new Point(encabezado.Width - salir.Width - 18, (encabezado.Height - salir.Height) / 2);
            rol.Location = new Point(salir.Left - rol.Width - 16, (encabezado.Height - rol.Height) / 2);
            marca.Location = new Point(18, (encabezado.Height - marca.Height) / 2);
        };

        // Barra lateral
        var lateral = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = Tema.Lateral,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
            AutoScroll = true,
        };
        var atajo = 1;
        foreach (var (clave, texto, tipo) in Menu)
        {
            var pantalla = (PantallaBase)ActivatorUtilities.CreateInstance(servicios, tipo);
            pantalla.Visible = false;
            _pantallas[clave] = pantalla;
            _contenedor.Controls.Add(pantalla);

            var boton = new Button
            {
                Text = "   " + texto,
                Width = 220,
                Height = 42,
                Margin = Padding.Empty,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Tema.Lateral,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Tema.Base,
                Cursor = Cursors.Hand,
                AccessibleName = texto,
                AccessibleDescription = atajo <= 9 ? $"Atajo Ctrl+{atajo}" : null,
                Tag = clave,
            };
            boton.FlatAppearance.BorderSize = 0;
            boton.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 58, 94);
            boton.Click += (_, _) => Mostrar(clave);
            _botones[clave] = boton;
            lateral.Controls.Add(boton);
            atajo++;
        }

        // Barra de estado con métricas del Singleton (mismo objeto que muestra el Panel general).
        var estado = new StatusStrip { BackColor = Tema.Superficie, SizingGrip = true };
        estado.Items.Add(_estadoMetricas);
        estado.Items.Add(new ToolStripStatusLabel($"BD: {Path.GetFileName(servicios.GetRequiredService<Infrastructure.Persistencia.OpcionesLogiMarket>().RutaBaseDatos)}"));
        _metricas.Cambiaron += AlCambiarMetricas;
        PintarMetricas(_metricas.Instantanea);

        Controls.Add(_contenedor);
        Controls.Add(lateral);
        Controls.Add(encabezado);
        Controls.Add(estado);
    }

    /// <summary>True si el usuario pidió cerrar sesión (volver al acceso) en vez de salir.</summary>
    public bool CerroSesion { get; private set; }

    public void Mostrar(string clave)
    {
        if (!_pantallas.TryGetValue(clave, out var destino) || destino == _actual)
        {
            return;
        }

        foreach (var (k, b) in _botones)
        {
            b.BackColor = k == clave ? Tema.LateralActivo : Tema.Lateral;
            b.Font = k == clave ? Tema.Negrita : Tema.Base;
        }

        if (_actual is not null)
        {
            _actual.Visible = false;
        }

        _actual = destino;
        destino.Visible = true;
        destino.BringToFront();
        destino.SelectNextControl(destino, true, true, true, true);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Mostrar(ClavesPantalla.Panel);
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Control && e.KeyCode is >= Keys.D1 and <= Keys.D9)
        {
            Mostrar(Menu[e.KeyCode - Keys.D1].Clave);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F5 && _actual is not null)
        {
            await _actual.ActualizarAsync();
            e.Handled = true;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _metricas.Cambiaron -= AlCambiarMetricas;
        base.OnFormClosed(e);
    }

    private void CerrarSesion()
    {
        if (!EjecutorOperaciones.Confirmar(this, "¿Cerrar la sesión actual?", "Cerrar sesión"))
        {
            return;
        }

        _ = _ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioAutenticacion>().CerrarAsync());
        CerroSesion = true;
        Close();
    }

    private void AlCambiarMetricas(object? sender, InstantaneaMetricas m)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => PintarMetricas(m));
        }
        else
        {
            PintarMetricas(m);
        }
    }

    private void PintarMetricas(InstantaneaMetricas m) =>
        _estadoMetricas.Text = $"MetricasSesion #{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_metricas):X8} · {m.PedidosConfirmados} pedidos · {Tema.Usd(m.VentasUsd)} · {m.Despachos} despachos · {m.Devoluciones} devoluciones · {m.CobrosDenegados} cobros denegados · {m.Notificaciones} avisos · {m.ComandosEjecutados} comandos";
}
