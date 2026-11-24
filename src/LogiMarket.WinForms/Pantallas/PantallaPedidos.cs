using LogiMarket.Application.Comandos;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Componentes;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>Pantalla 06 — Pedidos e historial: filtro con el lenguaje del Interpreter y línea de tiempo de estados.</summary>
public sealed class PantallaPedidos : PantallaBase
{
    private static readonly string[] Flujo = ["BORRADOR", "VALIDADO", "PAGADO", "DESPACHADO", "ENTREGADO"];

    private readonly TextBox _filtro = new() { Width = 420, AccessibleName = "Filtro de pedidos", PlaceholderText = "ESTADO = PAGADO AND TOTAL > 50" };
    private readonly GrillaDatos _grilla = new() { AccessibleName = "Pedidos" };
    private readonly FlowLayoutPanel _linea = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 4, 0, 8) };
    private readonly GrillaDatos _lineas = new() { AccessibleName = "Líneas del pedido", Height = 140, Dock = DockStyle.Top };
    private readonly GrillaDatos _historial = new() { AccessibleName = "Historial de estados" };
    private readonly Label _detalle = new() { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 4, 0, 4) };
    private readonly Label _resultado = new() { AutoSize = true, ForeColor = Tema.TextoSuave };
    private readonly Button _cancelar;
    private readonly Button _cobrar;

    public PantallaPedidos(EjecutorOperaciones ejecutor, IMediadorPantallas mediador)
        : base(ClavesPantalla.Pedidos, "Pedidos e historial", "Filtro (Interpreter): ESTADO = PAGADO · CLIENTE = \"Ana López\" · TOTAL > 50 · con AND, OR y paréntesis.", ejecutor, mediador)
    {
        _grilla.Columna("Código", nameof(PedidoResumenDto.Codigo), peso: 55)
            .Columna("Cliente", nameof(PedidoResumenDto.Cliente), peso: 110)
            .Columna("Categoría", nameof(PedidoResumenDto.Categoria), peso: 60)
            .Columna("Estado", nameof(PedidoResumenDto.Estado), peso: 70)
            .Columna("Entrega", nameof(PedidoResumenDto.Entrega), peso: 55)
            .Columna("Total", nameof(PedidoResumenDto.Total), FormatoColumna.Moneda, 65)
            .Columna("Fecha", nameof(PedidoResumenDto.FechaCreacionUtc), FormatoColumna.Fecha, 80);
        _grilla.ClaveDe = o => ((PedidoResumenDto)o).Id;
        _grilla.SelectionChanged += async (_, _) => await MostrarDetalleAsync();
        _lineas.Columna("Código", nameof(LineaPedidoDto.Codigo), peso: 50)
            .Columna("Artículo", nameof(LineaPedidoDto.Nombre), peso: 100)
            .Columna("Cant.", nameof(LineaPedidoDto.Cantidad), FormatoColumna.Entero, 30)
            .Columna("P. unitario", nameof(LineaPedidoDto.PrecioUnitario), FormatoColumna.Moneda, 60)
            .Columna("Componentes (instantánea)", nameof(LineaPedidoDto.Componentes), peso: 110);
        _historial.Columna("De", nameof(HistorialDto.Anterior), peso: 60)
            .Columna("A", nameof(HistorialDto.Nuevo), peso: 60)
            .Columna("Usuario", nameof(HistorialDto.Usuario), peso: 70)
            .Columna("Fecha", nameof(HistorialDto.FechaUtc), FormatoColumna.Fecha, 80);
        _filtro.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await ActualizarAsync();
            }
        };

        _cancelar = Tema.Boton("Cancelar pedido", async (_, _) => await CancelarAsync());
        _cobrar = Tema.Boton("Ir a cobro", async (_, _) =>
        {
            if (_grilla.Seleccionado<PedidoResumenDto>() is { } p)
            {
                await NotificarAsync(EventoPantalla.AbrirPedido, (ClavesPantalla.Cobro, p.Id));
            }
        });

        var chips = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        foreach (var estado in Flujo.Append("CANCELADO"))
        {
            var chip = Tema.Chip(estado, Tema.ColorEstado(estado));
            chip.Cursor = Cursors.Hand;
            chip.AccessibleName = $"Filtrar {estado}";
            chip.Click += async (_, _) =>
            {
                _filtro.Text = $"ESTADO = {estado}";
                await ActualizarAsync();
            };
            chips.Controls.Add(chip);
        }

        var detalle = new Panel { Dock = DockStyle.Fill };
        detalle.Resize += (_, _) => _detalle.MaximumSize = new Size(Math.Max(200, detalle.ClientSize.Width - 8), 0);
        detalle.Controls.Add(_historial);
        detalle.Controls.Add(_lineas);
        detalle.Controls.Add(_detalle);
        detalle.Controls.Add(_linea);
        detalle.Controls.Add(Tema.Fila(_cobrar, _cancelar));

        var division = new SplitContainer { Dock = DockStyle.Fill };
        division.Panel1.Controls.Add(Tema.Tarjeta("Pedidos", _grilla));
        division.Panel2.Controls.Add(Tema.Tarjeta("Historial seleccionado", detalle));
        Contenido.Controls.Add(division);
        Contenido.Controls.Add(chips);
        Contenido.Controls.Add(Tema.Fila(Tema.Etiqueta("&Filtro:"), _filtro, Tema.Boton("Filtrar", async (_, _) => await ActualizarAsync(), primario: true), Tema.Boton("Limpiar", async (_, _) =>
        {
            _filtro.Clear();
            await ActualizarAsync();
        }), _resultado));
        Resize += (_, _) => division.SplitterDistance = Math.Max(300, (int)(division.Width * 0.52));
    }

    public override void Recibir(EventoPantalla evento, object? dato)
    {
        if (dato is (string, int id))
        {
            _filtro.Clear();
            _grilla.ClaveDe = o => ((PedidoResumenDto)o).Id;
            _seleccionPendiente = id;
        }
    }

    private int? _seleccionPendiente;

    protected override async Task CargarAsync()
    {
        var filtro = _filtro.Text;
        var (ok, pedidos) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ConsultasOperativas>().PedidosAsync(filtro));
        if (!ok)
        {
            _resultado.Text = "Filtro no aplicado.";
            return;
        }

        _grilla.Mostrar(pedidos!);
        _resultado.Text = $"{pedidos!.Count} pedido(s)";
        if (_seleccionPendiente is int id)
        {
            _seleccionPendiente = null;
            foreach (DataGridViewRow fila in _grilla.Rows)
            {
                if (fila.DataBoundItem is PedidoResumenDto p && p.Id == id)
                {
                    _grilla.CurrentCell = fila.Cells[0];
                }
            }
        }

        await MostrarDetalleAsync();
    }

    private async Task MostrarDetalleAsync()
    {
        var p = _grilla.Seleccionado<PedidoResumenDto>();
        _linea.Controls.Clear();
        if (p is null)
        {
            _detalle.Text = "Seleccione un pedido.";
            _lineas.Mostrar(Array.Empty<LineaPedidoDto>());
            _historial.Mostrar(Array.Empty<HistorialDto>());
            _cancelar.Enabled = _cobrar.Enabled = false;
            return;
        }

        var (ok, d) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ConsultasOperativas>().PedidoAsync(p.Id));
        if (!ok || d is null || _grilla.Seleccionado<PedidoResumenDto>()?.Id != p.Id)
        {
            return;
        }

        var alcanzados = d.Historial.Select(h => h.Nuevo).ToHashSet();
        foreach (var estado in Flujo)
        {
            var activo = alcanzados.Contains(estado);
            _linea.Controls.Add(Tema.Chip(estado, activo ? Tema.ColorEstado(estado) : Color.FromArgb(203, 213, 225)));
            if (estado != Flujo[^1])
            {
                _linea.Controls.Add(Tema.Etiqueta("→", color: Tema.TextoSuave));
            }
        }

        if (p.Estado == "CANCELADO")
        {
            _linea.Controls.Add(Tema.Chip("CANCELADO", Tema.Peligro));
        }

        if (p.Devuelto)
        {
            _linea.Controls.Add(Tema.Chip($"DEVUELTO {Tema.Usd(p.Reembolso)}", Tema.Alerta));
        }

        _detalle.Text = $"{p.Codigo} · {p.Cliente} ({p.Categoria}) · Política {p.Politica}\n"
            + $"Subtotal {Tema.Usd(p.Subtotal)} · Descuento −{Tema.Usd(p.Descuento)} · Entrega {Tema.Usd(p.Tarifa)} · TOTAL {Tema.Usd(p.Total)}\n"
            + $"Cobro: {d.ReferenciaCobro ?? "—"} {(d.Proveedor is null ? string.Empty : $"({d.Proveedor})")} · Guía: {d.Despacho?.Guia ?? "—"}";
        _lineas.Mostrar(d.Lineas);
        _historial.Mostrar(d.Historial);
        _cancelar.Enabled = Puede(Operacion.CancelarPedido) && p.Estado is "BORRADOR" or "VALIDADO";
        _cobrar.Enabled = p.Estado == "VALIDADO" && Puede(Operacion.CobrarPedido);
    }

    private async Task CancelarAsync()
    {
        if (_grilla.Seleccionado<PedidoResumenDto>() is not { } p
            || !EjecutorOperaciones.Confirmar(this, $"¿Cancelar el pedido {p.Codigo}? Esta acción es irreversible.", "Cancelar pedido"))
        {
            return;
        }

        var invocador = Ejecutor.Global<InvocadorComandos>();
        var (ok, r) = await Ejecutor.IntentarAsync(this, sp => invocador.EjecutarAsync(sp.GetRequiredService<FabricaComandos>().Cancelar(p.Id)));
        if (ok && EjecutorOperaciones.Informar(this, r!, "Cancelar pedido"))
        {
            await NotificarAsync(EventoPantalla.PedidoActualizado, p.Id);
        }
    }
}
