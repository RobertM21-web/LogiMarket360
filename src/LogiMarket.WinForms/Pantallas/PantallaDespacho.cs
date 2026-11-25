using LogiMarket.Application.Comandos;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Componentes;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Domain.Pedidos.Estados;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>Pantalla 08 — Despacho y entrega: solo transiciones válidas (State) y autorizadas.</summary>
public sealed class PantallaDespacho : PantallaBase
{
    private readonly GrillaDatos _grilla = new() { AccessibleName = "Pedidos para logística" };
    private readonly CheckBox _verEntregados = new() { Text = "Mostrar entregados", AutoSize = true };
    private readonly Label _pedido = Tema.Valor(negrita: true);
    private readonly Label _estado = Tema.Valor(negrita: true);
    private readonly Label _entrega = Tema.Valor();
    private readonly Label _guia = Tema.Valor(negrita: true);
    private readonly Label _plazo = Tema.Valor();
    private readonly Label _estimada = Tema.Valor();
    private readonly Label _entregado = Tema.Valor();
    private readonly TextBox _etiqueta = new() { Multiline = true, ReadOnly = true, Font = Tema.Monoespaciada, Height = 110, Dock = DockStyle.Top, AccessibleName = "Etiqueta de envío" };
    private readonly Button _despachar;
    private readonly Button _entregar;

    public PantallaDespacho(EjecutorOperaciones ejecutor, IMediadorPantallas mediador)
        : base(ClavesPantalla.Despacho, "Despacho y entrega", "La guía, la tarifa y el plazo provienen de la familia de entrega del pedido (Estándar o Exprés).", ejecutor, mediador)
    {
        _grilla.Columna("Pedido", nameof(PedidoResumenDto.Codigo), peso: 55)
            .Columna("Cliente", nameof(PedidoResumenDto.Cliente), peso: 110)
            .Columna("Estado", nameof(PedidoResumenDto.Estado), peso: 70)
            .Columna("Entrega", nameof(PedidoResumenDto.Entrega), peso: 60)
            .Columna("Guía", nameof(PedidoResumenDto.Guia), peso: 90)
            .Columna("Pagado", nameof(PedidoResumenDto.FechaPagoUtc), FormatoColumna.Fecha, 80);
        _grilla.ClaveDe = o => ((PedidoResumenDto)o).Id;
        _grilla.SelectionChanged += async (_, _) => await MostrarDetalleAsync();
        _verEntregados.CheckedChanged += async (_, _) => await ActualizarAsync();

        _despachar = Tema.Boton("Marcar como &despachado", async (_, _) => await DespacharAsync(), primario: true);
        _entregar = Tema.Boton("Registrar &entrega", async (_, _) => await EntregarAsync());

        var detalle = new Panel { Dock = DockStyle.Fill };
        detalle.Controls.Add(_etiqueta);
        detalle.Controls.Add(Tema.Fila(_despachar, _entregar));
        detalle.Controls.Add(Tema.Ficha(("Pedido", _pedido), ("Estado", _estado), ("Entrega", _entrega), ("Guía simulada", _guia), ("Plazo", _plazo), ("Fecha estimada", _estimada), ("Entregado", _entregado)));

        var division = new SplitContainer { Dock = DockStyle.Fill };
        division.Panel1.Controls.Add(Tema.Tarjeta("Pedidos pagados y despachados", _grilla));
        division.Panel2.Controls.Add(Tema.Tarjeta("Detalle logístico", detalle));
        Contenido.Controls.Add(division);
        Contenido.Controls.Add(Tema.Fila(_verEntregados, Tema.Boton("Actualizar (F5)", async (_, _) => await ActualizarAsync())));
        Resize += (_, _) => division.SplitterDistance = Math.Max(300, (int)(division.Width * 0.58));
    }

    protected override async Task CargarAsync()
    {
        var filtro = _verEntregados.Checked ? "ESTADO = PAGADO OR ESTADO = DESPACHADO OR ESTADO = ENTREGADO" : "ESTADO = PAGADO OR ESTADO = DESPACHADO";
        var (ok, pedidos) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ConsultasOperativas>().PedidosAsync(filtro));
        if (ok)
        {
            _grilla.Mostrar(pedidos!.OrderBy(p => p.Estado == NombresEstado.Pagado ? 0 : p.Estado == NombresEstado.Despachado ? 1 : 2).ThenBy(p => p.Id));
            await MostrarDetalleAsync();
        }
    }

    private async Task MostrarDetalleAsync()
    {
        var p = _grilla.Seleccionado<PedidoResumenDto>();
        _despachar.Enabled = p?.Estado == NombresEstado.Pagado && Puede(Operacion.DespacharPedido);
        _entregar.Enabled = p?.Estado == NombresEstado.Despachado && Puede(Operacion.EntregarPedido);
        if (p is null)
        {
            foreach (var l in new[] { _pedido, _estado, _entrega, _guia, _plazo, _estimada, _entregado })
            {
                l.Text = "—";
            }

            _etiqueta.Clear();
            return;
        }

        var (ok, d) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ConsultasOperativas>().PedidoAsync(p.Id));
        if (!ok || d is null)
        {
            return;
        }

        _pedido.Text = $"{p.Codigo} · {p.Cliente}";
        _estado.Text = p.Estado;
        _estado.ForeColor = Tema.ColorEstado(p.Estado);
        _entrega.Text = $"{p.Entrega} · tarifa {Tema.Usd(p.Tarifa)}";
        _guia.Text = d.Despacho?.Guia ?? "(se genera al despachar)";
        _plazo.Text = d.Despacho is null ? "—" : $"{d.Despacho.PlazoDias} día(s)";
        _estimada.Text = Tema.Fecha(d.Despacho?.FechaEstimadaUtc);
        _entregado.Text = Tema.Fecha(d.Despacho?.FechaEntregaUtc);
        _etiqueta.Text = d.Despacho?.Etiqueta ?? string.Empty;
    }

    private async Task DespacharAsync()
    {
        if (_grilla.Seleccionado<PedidoResumenDto>() is not { } p
            || !EjecutorOperaciones.Confirmar(this, $"¿Marcar {p.Codigo} como DESPACHADO? Se generará la guía de envío.", "Despacho"))
        {
            return;
        }

        var invocador = Ejecutor.Global<InvocadorComandos>();
        var (ok, r) = await Ejecutor.IntentarAsync(this, sp => invocador.EjecutarAsync(sp.GetRequiredService<FabricaComandos>().Despachar(p.Id)));
        if (ok && EjecutorOperaciones.Informar(this, r!, "Despacho"))
        {
            await NotificarAsync(EventoPantalla.PedidoActualizado, p.Id);
        }
    }

    private async Task EntregarAsync()
    {
        if (_grilla.Seleccionado<PedidoResumenDto>() is not { } p
            || !EjecutorOperaciones.Confirmar(this, $"¿Registrar la entrega de {p.Codigo}? Desde esta fecha corre el plazo de devolución.", "Entrega"))
        {
            return;
        }

        var (ok, r) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioLogistica>().EntregarAsync(p.Id));
        if (ok && EjecutorOperaciones.Informar(this, r!, "Entrega"))
        {
            await NotificarAsync(EventoPantalla.PedidoActualizado, p.Id);
        }
    }
}
