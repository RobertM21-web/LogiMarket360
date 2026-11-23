using LogiMarket.Application.Comandos;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Demostracion;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>
/// Pantalla 07 — Confirmación y cobro. Ejecuta <see cref="ConfirmarPedidoCommand"/> (Facade → Proxy →
/// Decorator → Adapter/V2) con clave de idempotencia: repetir no cobra dos veces.
/// </summary>
public sealed class PantallaCobro : PantallaBase
{
    private sealed record OpcionPedido(PedidoResumenDto Pedido)
    {
        public override string ToString() => $"{Pedido.Codigo} · {Pedido.Cliente} · {Tema.Usd(Pedido.Total)} · {Pedido.Estado}";
    }

    private readonly ComboBox _pedido = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420, AccessibleName = "Pedido a cobrar" };
    private readonly CheckBox _incluirPagados = new() { Text = "Mostrar también pagados (para comprobar cobro único)", AutoSize = true };
    private readonly Label _cliente = Tema.Valor();
    private readonly Label _estado = Tema.Valor(negrita: true);
    private readonly Label _subtotal = Tema.Valor();
    private readonly Label _descuento = Tema.Valor();
    private readonly Label _entrega = Tema.Valor();
    private readonly Label _total = new() { AutoSize = true, Font = Tema.Kpi, ForeColor = Tema.Primario };
    private readonly ComboBox _proveedor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, AccessibleName = "Proveedor simulado" };
    private readonly FlowLayoutPanel _chips = new() { AutoSize = true, Dock = DockStyle.Top };
    private readonly TextBox _resultado = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = Tema.Monoespaciada, AccessibleName = "Resultado del cobro", Height = 170, Dock = DockStyle.Top };
    private readonly Button _confirmar;
    private int? _pedidoPreferido;

    public PantallaCobro(EjecutorOperaciones ejecutor, IMediadorPantallas mediador, ISeleccionProveedorPago proveedores)
        : base(ClavesPantalla.Cobro, "Confirmación y cobro", "El pedido se revalida antes de cobrar; stock, pago y evento se registran en una sola transacción.", ejecutor, mediador)
    {
        _proveedor.Items.AddRange(proveedores.Disponibles.Cast<object>().ToArray());
        _proveedor.SelectedItem = proveedores.ProveedorActivo;
        _proveedor.SelectionChangeCommitted += async (_, _) =>
        {
            var elegido = (string)_proveedor.SelectedItem!;
            if (!await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioDemostracion>().SeleccionarProveedorAsync(elegido)))
            {
                _proveedor.SelectedItem = proveedores.ProveedorActivo;
            }
        };
        _pedido.SelectedIndexChanged += (_, _) => MostrarPedido();
        _incluirPagados.CheckedChanged += async (_, _) => await ActualizarAsync();
        _confirmar = Tema.Boton("&Confirmar cobro", async (_, _) => await ConfirmarAsync(), primario: true);

        var tarjetaPedido = new Panel { Dock = DockStyle.Fill };
        tarjetaPedido.Controls.Add(_total);
        _total.Dock = DockStyle.Top;
        tarjetaPedido.Controls.Add(Tema.Ficha(("Cliente", _cliente), ("Estado", _estado), ("Subtotal", _subtotal), ("Descuento", _descuento), ("Entrega", _entrega)));

        var pago = new Panel { Dock = DockStyle.Fill };
        pago.Controls.Add(_resultado);
        pago.Controls.Add(Tema.Etiqueta("Resultado / referencia del cobro:", Tema.Negrita));
        pago.Controls.Add(Tema.Fila(_confirmar));
        pago.Controls.Add(_chips);
        pago.Controls.Add(Tema.Fila(Tema.Etiqueta("&Proveedor simulado:"), _proveedor));
        foreach (Control c in pago.Controls)
        {
            c.Dock = DockStyle.Top;
        }

        var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabla.Controls.Add(Tema.Tarjeta("Pedido", tarjetaPedido), 0, 0);
        tabla.Controls.Add(Tema.Tarjeta("Proceso de pago", pago), 1, 0);
        Contenido.Controls.Add(tabla);
        Contenido.Controls.Add(Tema.Fila(Tema.Etiqueta("Pe&dido:"), _pedido, _incluirPagados));
    }

    public override void Recibir(EventoPantalla evento, object? dato)
    {
        _pedidoPreferido = dato switch
        {
            int id => id,
            (string, int id) => id,
            _ => _pedidoPreferido,
        };
    }

    protected override void AplicarPermisos()
    {
        _chips.Controls.Clear();
        var rol = Usuario?.Rol.ToString().ToUpperInvariant() ?? "—";
        var puede = Puede(Operacion.CobrarPedido);
        _chips.Controls.Add(Tema.Chip($"ROL {rol}", puede ? Tema.Primario : Tema.Peligro));
        _chips.Controls.Add(Tema.Chip("COBRO ÚNICO", Tema.Exito));
        if (!puede)
        {
            _chips.Controls.Add(Tema.Etiqueta("Su rol no puede cobrar (Application lo bloquea y lo audita).", color: Tema.Peligro));
        }

        var demo = Ejecutor.Global<IModoDemostracion>();
        if (demo.RechazarCobros)
        {
            _chips.Controls.Add(Tema.Chip("MODO DEMO: RECHAZO INDUCIDO", Tema.Alerta));
        }

        _proveedor.SelectedItem = Ejecutor.Global<ISeleccionProveedorPago>().ProveedorActivo;
        _proveedor.Enabled = puede;
    }

    protected override async Task CargarAsync()
    {
        AplicarPermisos();
        var (ok, pedidos) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ConsultasOperativas>().PedidosAsync("ESTADO = VALIDADO OR ESTADO = PAGADO"));
        if (!ok)
        {
            return;
        }

        // Pendientes de cobro; los pagados solo si se piden o si es el pedido recién enviado/cobrado (para ver su referencia).
        var anterior = _pedidoPreferido ?? (_pedido.SelectedItem as OpcionPedido)?.Pedido.Id;
        _pedidoPreferido = null;
        var visibles = pedidos!.Where(p => p.Estado == "VALIDADO" || _incluirPagados.Checked || p.Id == anterior);
        _pedido.Items.Clear();
        _pedido.Items.AddRange(visibles.OrderByDescending(p => p.Id).Select(p => (object)new OpcionPedido(p)).ToArray());
        _pedido.SelectedItem = _pedido.Items.Cast<OpcionPedido>().FirstOrDefault(o => o.Pedido.Id == anterior) ?? _pedido.Items.Cast<object>().FirstOrDefault();
        MostrarPedido();
    }

    private void MostrarPedido()
    {
        var p = (_pedido.SelectedItem as OpcionPedido)?.Pedido;
        _cliente.Text = p is null ? "—" : $"{p.Cliente} · {p.Categoria}";
        _estado.Text = p?.Estado ?? "Sin pedidos validados";
        _estado.ForeColor = p is null ? Tema.TextoSuave : Tema.ColorEstado(p.Estado);
        _subtotal.Text = p is null ? "—" : Tema.Usd(p.Subtotal);
        _descuento.Text = p is null ? "—" : $"−{Tema.Usd(p.Descuento)} ({p.Politica})";
        _entrega.Text = p is null ? "—" : $"{Tema.Usd(p.Tarifa)} ({p.Entrega})";
        _total.Text = "TOTAL " + Tema.Usd(p?.Total ?? 0);
        _confirmar.Enabled = p is not null;
    }

    private async Task ConfirmarAsync()
    {
        if (_pedido.SelectedItem is not OpcionPedido { Pedido: var p }
            || !EjecutorOperaciones.Confirmar(this, $"¿Confirmar y cobrar {Tema.Usd(p.Total)} del pedido {p.Codigo} con el proveedor {_proveedor.SelectedItem}?", "Confirmar cobro"))
        {
            return;
        }

        var invocador = Ejecutor.Global<InvocadorComandos>();
        ConfirmarPedidoCommand? comando = null;
        var (ok, r) = await Ejecutor.IntentarAsync(this, sp =>
        {
            comando = sp.GetRequiredService<FabricaComandos>().Confirmar(p.Id);
            return invocador.EjecutarAsync(comando);
        });
        if (!ok || r is null)
        {
            return;
        }

        var d = comando?.Detalle;
        var lineas = new List<string>
        {
            $"[{DateTime.Now:HH:mm:ss}] {p.Codigo} → {(r.Exito ? (d?.EsRepeticion == true ? "YA COBRADO (repetición idempotente)" : "COBRO APROBADO") : r.Codigo)}",
            r.Mensaje,
        };
        if (d?.ReferenciaCobro is not null)
        {
            lineas.Add($"Referencia: {d.ReferenciaCobro} · Total {Tema.Usd(d.Total)}");
        }

        if (d?.RecorridoValidacion.Count > 0)
        {
            lineas.Add("Validación: " + string.Join(" → ", d.RecorridoValidacion));
        }

        _resultado.Text = string.Join(Environment.NewLine, lineas) + Environment.NewLine + new string('─', 48) + Environment.NewLine + _resultado.Text;
        _resultado.ForeColor = r.Exito ? Tema.Exito : Tema.Peligro;
        if (r.Exito)
        {
            _pedidoPreferido = p.Id;
            await NotificarAsync(EventoPantalla.PedidoConfirmado, p.Id);
            await ActualizarAsync();
        }
        else
        {
            await NotificarAsync(EventoPantalla.PedidoActualizado, p.Id);
        }
    }
}
