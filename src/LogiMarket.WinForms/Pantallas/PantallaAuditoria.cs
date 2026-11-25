using LogiMarket.Application.Comandos;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Demostracion;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Componentes;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>
/// Pantalla 12 — Auditoría y simulación: bitácoras (auditoría, Outbox, avisos, comandos) y modo demostración
/// con dobles de prueba reales (no alteran reglas ni falsean resultados).
/// </summary>
public sealed class PantallaAuditoria : PantallaBase
{
    private readonly ComboBox _categoria = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, AccessibleName = "Categoría de auditoría" };
    private readonly GrillaDatos _auditoria = new() { AccessibleName = "Registro de eventos" };
    private readonly GrillaDatos _outbox = new() { AccessibleName = "Eventos Outbox" };
    private readonly GrillaDatos _avisos = new() { AccessibleName = "Notificaciones" };
    private readonly GrillaDatos _comandos = new() { AccessibleName = "Historial de comandos" };
    private readonly RadioButton _legacy = new() { Text = "Proveedor Legacy", AutoSize = true, Checked = true };
    private readonly RadioButton _v2 = new() { Text = "Proveedor V2", AutoSize = true };
    private readonly CheckBox _rechazo = new() { Text = "Inducir rechazo de cobro", AutoSize = true };
    private readonly CheckBox _fallo = new() { Text = "Inducir fallo de Outbox (servicio de avisos caído)", AutoSize = true };
    private readonly CheckBox _reprocesar = new() { Text = "Reprocesar eventos pendientes", AutoSize = true };
    private readonly Label _estado = new() { AutoSize = true, ForeColor = Tema.TextoSuave, MaximumSize = new Size(360, 0) };
    private readonly Button _aplicar;
    private readonly Button _restablecer;
    private List<AuditoriaDto> _registros = [];

    public PantallaAuditoria(EjecutorOperaciones ejecutor, IMediadorPantallas mediador)
        : base(ClavesPantalla.Auditoria, "Auditoría y simulación", "Bitácora persistente independiente de la transacción de negocio. El modo demostración solo está disponible para ADMIN.", ejecutor, mediador)
    {
        _categoria.Items.AddRange(["(todas)", "PAGO", "SEGURIDAD", "NEGOCIO", "OUTBOX", "COMANDO", "INVENTARIO", "SISTEMA"]);
        _categoria.SelectedIndex = 0;
        _categoria.SelectedIndexChanged += (_, _) => Filtrar();

        _auditoria.Columna("Hora", nameof(AuditoriaDto.FechaUtc), FormatoColumna.Fecha, 70)
            .Columna("Categoría", nameof(AuditoriaDto.Categoria), peso: 55)
            .Columna("Evento", nameof(AuditoriaDto.Evento), peso: 90)
            .Columna("Resultado", nameof(AuditoriaDto.Resultado), peso: 70)
            .Columna("Detalle", nameof(AuditoriaDto.Detalle), peso: 170)
            .Columna("Usuario", nameof(AuditoriaDto.Usuario), peso: 55)
            .Columna("Correlación", nameof(AuditoriaDto.Correlacion), peso: 70);
        _auditoria.ClaveDe = o => ((AuditoriaDto)o).Id;
        _auditoria.CellFormatting += (_, e) =>
        {
            if (_auditoria.Columns[e.ColumnIndex].Name == nameof(AuditoriaDto.Resultado) && e.Value is string r)
            {
                e.CellStyle!.ForeColor = r.Contains("DENEGADO") || r.Contains("RECHAZ") || r.Contains("ERROR") ? Tema.Peligro
                    : r.Contains("PENDIENTE") || r.Contains("COMPENSADO") ? Tema.Alerta
                    : Tema.Exito;
            }
        };
        _outbox.Columna("Id", nameof(OutboxDto.Id), FormatoColumna.Entero, 25)
            .Columna("Tipo", nameof(OutboxDto.Tipo), peso: 70)
            .Columna("Referencia", nameof(OutboxDto.Referencia), peso: 55)
            .Columna("Estado", nameof(OutboxDto.Estado), peso: 55)
            .Columna("Intentos", nameof(OutboxDto.Intentos), FormatoColumna.Entero, 35)
            .Columna("Observadores completados", nameof(OutboxDto.Observadores), peso: 110)
            .Columna("Último error", nameof(OutboxDto.UltimoError), peso: 140)
            .Columna("Creado", nameof(OutboxDto.CreadoUtc), FormatoColumna.Fecha, 70);
        _avisos.Columna("Fecha", nameof(NotificacionDto.FechaUtc), FormatoColumna.Fecha, 70)
            .Columna("Canal", nameof(NotificacionDto.Canal), peso: 40)
            .Columna("Destinatario", nameof(NotificacionDto.Destinatario), peso: 100)
            .Columna("Asunto", nameof(NotificacionDto.Asunto), peso: 90)
            .Columna("Mensaje", nameof(NotificacionDto.Mensaje), peso: 200)
            .Columna("Referencia", nameof(NotificacionDto.Referencia), peso: 50);
        _comandos.Columna("Fecha", nameof(RegistroInvocacion.FechaUtc), FormatoColumna.Fecha, 70)
            .Columna("Usuario", nameof(RegistroInvocacion.Usuario), peso: 60)
            .Columna("Comando", nameof(RegistroInvocacion.Comando), peso: 80)
            .Columna("Entrada", nameof(RegistroInvocacion.Entrada), peso: 100)
            .Columna("Código", nameof(RegistroInvocacion.Codigo), peso: 70)
            .Columna("Mensaje", nameof(RegistroInvocacion.Mensaje), peso: 180);

        var pestanas = new TabControl { Dock = DockStyle.Fill };
        foreach (var (titulo, grilla) in new[] { ("Registro de eventos", _auditoria), ("Outbox", _outbox), ("Notificaciones", _avisos), ("Comandos (sesión)", _comandos) })
        {
            var pagina = new TabPage(titulo) { BackColor = Tema.Superficie, Padding = new Padding(4) };
            pagina.Controls.Add(grilla);
            pestanas.TabPages.Add(pagina);
        }

        _aplicar = Tema.Boton("&Aplicar escenario", async (_, _) => await AplicarAsync(), primario: true);
        _restablecer = Tema.Boton("Restablecer datos de prueba", async (_, _) => await RestablecerAsync());
        var demo = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        foreach (var c in new Control[] { _legacy, _v2, _rechazo, _fallo, _reprocesar, _estado })
        {
            c.MaximumSize = new Size(360, 0);
        }
        demo.Controls.AddRange([_legacy, _v2, _rechazo, _fallo, _reprocesar, _aplicar, _estado, new Label { Height = 16 }, _restablecer]);
        demo.Controls.Add(new Label
        {
            Text = "Los interruptores activan dobles de prueba (rechazo simulado del proveedor, avisos caídos). Las reglas de negocio y los cálculos no cambian; todo queda auditado.",
            ForeColor = Tema.TextoSuave,
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            Margin = new Padding(0, 12, 0, 0),
        });

        var division = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };
        division.Panel1.Controls.Add(pestanas);
        division.Panel2.Controls.Add(Tema.Tarjeta("Modo de demostración", demo));
        Contenido.Controls.Add(division);
        Contenido.Controls.Add(Tema.Fila(Tema.Etiqueta("&Categoría:"), _categoria, Tema.Boton("Actualizar (F5)", async (_, _) => await ActualizarAsync())));
        Resize += (_, _) => division.SplitterDistance = Math.Max(300, division.Width - 410);
    }

    protected override void AplicarPermisos()
    {
        var admin = Puede(Operacion.ModoDemostracion);
        foreach (var c in new Control[] { _legacy, _v2, _rechazo, _fallo, _reprocesar, _aplicar, _restablecer })
        {
            c.Enabled = admin;
        }

        if (!admin)
        {
            _estado.Text = "Solo ADMIN puede aplicar escenarios.";
        }
    }

    protected override async Task CargarAsync()
    {
        var (ok, datos) = await Ejecutor.IntentarAsync(this, async sp =>
        {
            var c = sp.GetRequiredService<ConsultasOperativas>();
            var escenario = sp.GetRequiredService<ServicioDemostracion>().Actual;
            return (await c.AuditoriaAsync(), await c.OutboxAsync(), await c.NotificacionesAsync(), escenario);
        });
        if (!ok)
        {
            return;
        }

        var (auditoria, outbox, avisos, escenario) = datos;
        _registros = auditoria.ToList();
        Filtrar();
        _outbox.Mostrar(outbox);
        _avisos.Mostrar(avisos);
        _comandos.Mostrar(Ejecutor.Global<InvocadorComandos>().Historial.Reverse());
        _legacy.Checked = escenario.Proveedor == "LEGACY";
        _v2.Checked = escenario.Proveedor == "V2";
        _rechazo.Checked = escenario.InducirRechazoCobro;
        _fallo.Checked = escenario.InducirFalloOutbox;
        var pendientes = outbox.Count(o => o.Estado != "PROCESADO");
        if (Puede(Operacion.ModoDemostracion))
        {
            _estado.Text = $"Activo: proveedor {escenario.Proveedor}; rechazo {(escenario.InducirRechazoCobro ? "SÍ" : "no")}; fallo Outbox {(escenario.InducirFalloOutbox ? "SÍ" : "no")}. Eventos pendientes: {pendientes}.";
        }
    }

    private void Filtrar()
    {
        var categoria = _categoria.SelectedIndex <= 0 ? null : (string)_categoria.SelectedItem!;
        _auditoria.Mostrar(_registros.Where(r => categoria is null || r.Categoria == categoria));
    }

    private async Task AplicarAsync()
    {
        var escenario = new EscenarioDemostracion(_v2.Checked ? "V2" : "LEGACY", _rechazo.Checked, _fallo.Checked, _reprocesar.Checked);
        var (ok, mensaje) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioDemostracion>().AplicarAsync(escenario));
        if (ok)
        {
            _reprocesar.Checked = false;
            MessageBox.Show(this, mensaje, "Modo de demostración", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await NotificarAsync(EventoPantalla.EscenarioAplicado);
            await ActualizarAsync();
        }
    }

    private async Task RestablecerAsync()
    {
        if (!EjecutorOperaciones.Confirmar(this, "Se eliminará la base local y se recreará con los datos de prueba.\nSe perderán pedidos, pagos y movimientos registrados. ¿Continuar?", "Restablecer datos de prueba"))
        {
            return;
        }

        var (ok, mensaje) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioDemostracion>().RestablecerDatosAsync());
        if (ok)
        {
            MessageBox.Show(this, mensaje, "Restablecer datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await NotificarAsync(EventoPantalla.CatalogoActualizado);
            await NotificarAsync(EventoPantalla.ClientesActualizados);
            await NotificarAsync(EventoPantalla.DevolucionRegistrada);
            await NotificarAsync(EventoPantalla.PedidoActualizado);
        }
    }
}
