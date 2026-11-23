using LogiMarket.Application.Comandos;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Devoluciones;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Pedidos;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>
/// Pantalla 09 — Devoluciones y reintegros (Template Method: DEFECTO ≤ 30 días; ARREPENTIMIENTO ≤ 7 días y sin uso).
/// El reembolso es el importe de productos tras descuento, sin envío.
/// </summary>
public sealed class PantallaDevoluciones : PantallaBase
{
    private sealed record OpcionPedido(PedidoResumenDto Pedido)
    {
        public override string ToString() => $"{Pedido.Codigo} · {Pedido.Cliente} · {Tema.Usd(Pedido.Total)}{(Pedido.Devuelto ? " · DEVUELTO" : string.Empty)}";
    }

    private readonly ComboBox _pedido = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380, AccessibleName = "Pedido entregado" };
    private readonly ComboBox _motivo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170, AccessibleName = "Motivo" };
    private readonly CheckBox _sinUso = new() { Text = "Producto sin uso", AutoSize = true, Checked = true };
    private readonly CheckBox _reintegrar = new() { Text = "Reintegrar stock (producto apto para la venta)", AutoSize = true, Checked = true };
    private readonly FlowLayoutPanel _chip = new() { AutoSize = true };
    private readonly Label _fechaEntrega = Tema.Valor();
    private readonly Label _dias = Tema.Valor();
    private readonly Label _plazo = Tema.Valor();
    private readonly Label _reintegro = Tema.Valor();
    private readonly Label _veredicto = new() { AutoSize = true, Font = Tema.Negrita, MaximumSize = new Size(460, 0) };
    private readonly Label _total = new() { AutoSize = true, Font = Tema.Kpi, ForeColor = Tema.Primario };
    private readonly ListBox _pasos = new() { IntegralHeight = false, AccessibleName = "Pasos del proceso" };
    private readonly Button _confirmar;

    public PantallaDevoluciones(EjecutorOperaciones ejecutor, IMediadorPantallas mediador)
        : base(ClavesPantalla.Devoluciones, "Devoluciones y reintegros", "Una sola devolución por pedido; el reembolso excluye el costo de envío.", ejecutor, mediador)
    {
        _motivo.Items.AddRange([MotivoDevolucion.Defecto, MotivoDevolucion.Arrepentimiento]);
        _motivo.SelectedIndex = 0;
        _pedido.SelectedIndexChanged += async (_, _) => await EvaluarAsync();
        _motivo.SelectedIndexChanged += async (_, _) => await EvaluarAsync();
        _sinUso.CheckedChanged += async (_, _) => await EvaluarAsync();
        _reintegrar.CheckedChanged += async (_, _) => await EvaluarAsync();
        _confirmar = Tema.Boton("&Confirmar devolución", async (_, _) => await ConfirmarAsync(), primario: true);

        var politica = new Panel { Dock = DockStyle.Fill };
        politica.Controls.Add(_veredicto);
        _veredicto.Dock = DockStyle.Top;
        politica.Controls.Add(Tema.Ficha(("Fecha de entrega", _fechaEntrega), ("Días transcurridos", _dias), ("Plazo del motivo", _plazo), ("Reintegro de stock", _reintegro)));
        politica.Controls.Add(Tema.Fila(_sinUso, _reintegrar));

        var resumen = new Panel { Dock = DockStyle.Fill };
        resumen.Controls.Add(_pasos);
        resumen.Controls.Add(Tema.Etiqueta("Pasos del proceso (Template Method):", Tema.Negrita));
        resumen.Controls.Add(Tema.Fila(_confirmar));
        resumen.Controls.Add(_total);
        resumen.Controls.Add(Tema.Etiqueta("Total del reembolso (productos tras descuento, sin envío):", color: Tema.TextoSuave));
        foreach (Control c in resumen.Controls)
        {
            c.Dock = c == _pasos ? DockStyle.Fill : DockStyle.Top;
        }

        var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabla.Controls.Add(Tema.Tarjeta("Validación de política", politica), 0, 0);
        tabla.Controls.Add(Tema.Tarjeta("Resumen", resumen), 1, 0);
        Contenido.Controls.Add(tabla);
        Contenido.Controls.Add(Tema.Fila(Tema.Etiqueta("&Pedido entregado:"), _pedido, Tema.Etiqueta("&Motivo:"), _motivo, _chip));
    }

    protected override void AplicarPermisos()
    {
        var puede = Puede(Operacion.ProcesarDevolucion);
        _motivo.Enabled = _sinUso.Enabled = _reintegrar.Enabled = puede;
        if (!puede)
        {
            _confirmar.Enabled = false;
        }
    }

    protected override async Task CargarAsync()
    {
        var (ok, pedidos) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ConsultasOperativas>().PedidosAsync("ESTADO = ENTREGADO"));
        if (!ok)
        {
            return;
        }

        var anterior = (_pedido.SelectedItem as OpcionPedido)?.Pedido.Id;
        _pedido.Items.Clear();
        _pedido.Items.AddRange(pedidos!.OrderBy(p => p.Devuelto).ThenByDescending(p => p.FechaEntregaUtc).Select(p => (object)new OpcionPedido(p)).ToArray());
        _pedido.SelectedItem = _pedido.Items.Cast<OpcionPedido>().FirstOrDefault(o => o.Pedido.Id == anterior) ?? _pedido.Items.Cast<object>().FirstOrDefault();
        if (_pedido.Items.Count == 0)
        {
            await EvaluarAsync();
        }
    }

    private SolicitudDevolucion? Solicitud() =>
        _pedido.SelectedItem is OpcionPedido o && _motivo.SelectedItem is MotivoDevolucion m
            ? new SolicitudDevolucion(o.Pedido.Id, m, _sinUso.Checked, _reintegrar.Checked)
            : null;

    private async Task EvaluarAsync()
    {
        _pasos.Items.Clear();
        _chip.Controls.Clear();
        _sinUso.Visible = _motivo.SelectedItem is MotivoDevolucion.Arrepentimiento;
        var solicitud = Solicitud();
        if (solicitud is null)
        {
            _veredicto.Text = "No hay pedidos ENTREGADOS para devolver.";
            _veredicto.ForeColor = Tema.TextoSuave;
            _total.Text = Tema.Usd(0);
            _confirmar.Enabled = false;
            return;
        }

        var (ok, e) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioDevoluciones>().EvaluarAsync(solicitud));
        if (!ok || e is null || Solicitud() != solicitud)
        {
            return;
        }

        _fechaEntrega.Text = Tema.Fecha(e.FechaEntregaUtc);
        _dias.Text = $"{e.DiasTranscurridos} día(s)";
        _plazo.Text = $"≤ {e.PlazoMaximoDias} días{(solicitud.Motivo == MotivoDevolucion.Arrepentimiento ? " y sin uso" : string.Empty)}";
        _reintegro.Text = e.ReintegraStock ? "Sí, se reintegra al inventario" : "No (producto no apto para la venta)";
        _veredicto.Text = e.Mensaje;
        _veredicto.ForeColor = e.Elegible ? Tema.Exito : Tema.Peligro;
        _chip.Controls.Add(Tema.Chip(e.Elegible ? "ELEGIBLE" : "NO ELEGIBLE", e.Elegible ? Tema.Exito : Tema.Peligro));
        _total.Text = Tema.Usd(e.Elegible ? e.Reembolso : 0);
        _confirmar.Enabled = e.Elegible && Puede(Operacion.ProcesarDevolucion);
    }

    private async Task ConfirmarAsync()
    {
        var solicitud = Solicitud();
        if (solicitud is null || _pedido.SelectedItem is not OpcionPedido { Pedido: var p }
            || !EjecutorOperaciones.Confirmar(this, $"¿Confirmar la devolución de {p.Codigo} por {solicitud.Motivo.ToString().ToUpperInvariant()} con reembolso de {_total.Text}?\n\nSolo se admite una devolución por pedido.", "Confirmar devolución"))
        {
            return;
        }

        var invocador = Ejecutor.Global<InvocadorComandos>();
        ProcesarDevolucionCommand? comando = null;
        var (ok, r) = await Ejecutor.IntentarAsync(this, sp =>
        {
            comando = sp.GetRequiredService<FabricaComandos>().Devolver(solicitud);
            return invocador.EjecutarAsync(comando);
        });
        if (!ok || r is null)
        {
            return;
        }

        _pasos.Items.Clear();
        _pasos.Items.AddRange((comando?.Detalle?.Pasos ?? []).Select(x => (object)x).ToArray());
        if (EjecutorOperaciones.Informar(this, r, "Devolución"))
        {
            await NotificarAsync(EventoPantalla.DevolucionRegistrada, p.Id);
            var pasos = _pasos.Items.Cast<object>().ToArray();
            await ActualizarAsync();
            _pasos.Items.AddRange(pasos);
        }
    }
}
