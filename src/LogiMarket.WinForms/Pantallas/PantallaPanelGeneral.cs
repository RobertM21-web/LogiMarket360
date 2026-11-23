using System.Runtime.CompilerServices;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Metricas;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Componentes;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>Pantalla 02 — Panel general con KPI reales, pedidos recientes, alertas y métricas de sesión.</summary>
public sealed class PantallaPanelGeneral : PantallaBase
{
    private readonly TarjetaKpi _activos = new("Pedidos activos");
    private readonly TarjetaKpi _porDespachar = new("Por despachar");
    private readonly TarjetaKpi _stockBajo = new("Stock bajo");
    private readonly TarjetaKpi _ventas = new("Ventas (USD)");
    private readonly GrillaDatos _recientes = new() { AccessibleName = "Pedidos recientes" };
    private readonly ListBox _alertas = new() { BorderStyle = BorderStyle.None, IntegralHeight = false, HorizontalScrollbar = true, AccessibleName = "Alertas" };
    private readonly Label _sesion = new() { AutoSize = true, Font = Tema.Monoespaciada };
    private readonly MetricasSesion _metricas;

    public PantallaPanelGeneral(EjecutorOperaciones ejecutor, IMediadorPantallas mediador, MetricasSesion metricas)
        : base(ClavesPantalla.Panel, "Panel general", "Indicadores calculados desde la base de datos y métricas de la sesión actual.", ejecutor, mediador)
    {
        _metricas = metricas;
        _recientes.Columna("Pedido", nameof(PedidoResumenDto.Codigo), peso: 70)
            .Columna("Cliente", nameof(PedidoResumenDto.Cliente))
            .Columna("Estado", nameof(PedidoResumenDto.Estado), peso: 70)
            .Columna("Total", nameof(PedidoResumenDto.Total), FormatoColumna.Moneda, 70)
            .Columna("Creado", nameof(PedidoResumenDto.FechaCreacionUtc), FormatoColumna.Fecha, 80);
        _recientes.ClaveDe = o => ((PedidoResumenDto)o).Id;
        _recientes.CellDoubleClick += async (_, _) =>
        {
            if (_recientes.Seleccionado<PedidoResumenDto>() is { } p)
            {
                await NotificarAsync(EventoPantalla.AbrirPedido, (ClavesPantalla.Pedidos, p.Id));
            }
        };

        var cuerpo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        cuerpo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cuerpo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cuerpo.Controls.Add(Tema.Tarjeta("Pedidos recientes (doble clic para ver historial)", _recientes), 0, 0);
        cuerpo.Controls.Add(Tema.Tarjeta("Alertas", _alertas), 1, 0);
        var sesion = Tema.Tarjeta("Métricas de la sesión (Singleton compartido con la barra de estado)", _sesion);
        sesion.Height = 104;
        sesion.Dock = DockStyle.Top;
        cuerpo.Controls.Add(sesion, 0, 1);
        cuerpo.SetColumnSpan(sesion, 2);

        Contenido.Controls.Add(cuerpo);
        Contenido.Controls.Add(TarjetaKpi.Fila(_activos, _porDespachar, _stockBajo, _ventas));
        Contenido.Controls.Add(Tema.Fila(Tema.Boton("Actualizar (F5)", async (_, _) => await ActualizarAsync())));

        _metricas.Cambiaron += AlCambiarMetricas;
        Disposed += (_, _) => _metricas.Cambiaron -= AlCambiarMetricas;
        PintarSesion(_metricas.Instantanea);
    }

    protected override async Task CargarAsync()
    {
        var (ok, panel) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ConsultasOperativas>().PanelAsync());
        if (!ok || panel is null)
        {
            return;
        }

        _activos.Valor = panel.PedidosActivos.ToString(Tema.Cultura);
        _porDespachar.Valor = panel.PorDespachar.ToString(Tema.Cultura);
        _stockBajo.Valor = panel.StockBajo.ToString(Tema.Cultura);
        _stockBajo.ColorValor = panel.StockBajo > 0 ? Tema.Alerta : Tema.Exito;
        _ventas.Valor = Tema.Usd(panel.VentasUsd);
        _recientes.Mostrar(panel.Recientes);
        _alertas.Items.Clear();
        _alertas.Items.AddRange(panel.Alertas.DefaultIfEmpty("Sin alertas: stock, Outbox y devoluciones en orden.").Cast<object>().ToArray());
    }

    private void AlCambiarMetricas(object? sender, InstantaneaMetricas m)
    {
        if (IsHandleCreated)
        {
            BeginInvoke(() => PintarSesion(m));
        }
    }

    private void PintarSesion(InstantaneaMetricas m) =>
        _sesion.Text = $"Instancia MetricasSesion #{RuntimeHelpers.GetHashCode(_metricas):X8} · inicio {Tema.Fecha(m.InicioSesionUtc)}\n"
            + $"Confirmados {m.PedidosConfirmados} · Ventas {Tema.Usd(m.VentasUsd)} · Despachos {m.Despachos} · Devoluciones {m.Devoluciones} · "
            + $"Cobros denegados {m.CobrosDenegados} · Avisos {m.Notificaciones} · Comandos {m.ComandosEjecutados}";
}
