using System.Diagnostics;
using LogiMarket.Application.Comprobantes;
using LogiMarket.Application.Consultas;
using LogiMarket.Infrastructure.Documentos;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Componentes;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>
/// Pantalla 11 — Reportes y comprobantes. Los comprobantes combinan tipo (Venta/Devolución) y salida
/// (Vista previa/TXT) en tiempo de ejecución (Bridge).
/// </summary>
public sealed class PantallaReportes : PantallaBase
{
    private const string VistaPrevia = "VISTA PREVIA";
    private const string ArchivoTxt = "ARCHIVO TXT";

    private readonly DateTimePicker _desde = new() { Format = DateTimePickerFormat.Short, Width = 120, AccessibleName = "Desde" };
    private readonly DateTimePicker _hasta = new() { Format = DateTimePickerFormat.Short, Width = 120, AccessibleName = "Hasta" };
    private readonly TarjetaKpi _confirmados = new("Pedidos confirmados");
    private readonly TarjetaKpi _netas = new("Ventas netas");
    private readonly TarjetaKpi _devoluciones = new("Devoluciones");
    private readonly GraficoBarras _grafico = new() { AccessibleName = "Ventas por día" };
    private readonly GrillaDatos _pedidos = new() { AccessibleName = "Pedidos del periodo" };
    private readonly ComboBox _tipo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, AccessibleName = "Tipo de comprobante" };
    private readonly TextBox _vista = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = Tema.Monoespaciada, AccessibleName = "Vista previa del comprobante" };
    private readonly LinkLabel _ruta = new() { AutoSize = true, Dock = DockStyle.Bottom, MaximumSize = new Size(520, 0) };
    private readonly SalidaVistaPrevia _salidaVista;
    private string? _ultimaRuta;

    public PantallaReportes(EjecutorOperaciones ejecutor, IMediadorPantallas mediador, SalidaVistaPrevia salidaVista)
        : base(ClavesPantalla.Reportes, "Reportes y comprobantes", "Ventas netas = ventas brutas − reembolsos del periodo. Seleccione un pedido para emitir su comprobante.", ejecutor, mediador)
    {
        _salidaVista = salidaVista;
        _desde.Value = DateTime.Today.AddDays(-6);
        _hasta.Value = DateTime.Today;
        _tipo.Items.AddRange([TipoComprobante.Venta, TipoComprobante.Devolucion]);
        _tipo.SelectedIndex = 0;
        _pedidos.Columna("Pedido", nameof(PedidoResumenDto.Codigo), peso: 50)
            .Columna("Cliente", nameof(PedidoResumenDto.Cliente), peso: 100)
            .Columna("Estado", nameof(PedidoResumenDto.Estado), peso: 65)
            .Columna("Total", nameof(PedidoResumenDto.Total), FormatoColumna.Moneda, 60)
            .Columna("Reembolso", nameof(PedidoResumenDto.Reembolso), FormatoColumna.Moneda, 60)
            .Columna("Pagado", nameof(PedidoResumenDto.FechaPagoUtc), FormatoColumna.Fecha, 75);
        _pedidos.ClaveDe = o => ((PedidoResumenDto)o).Id;
        _ruta.LinkClicked += (_, _) =>
        {
            if (_ultimaRuta is not null && File.Exists(_ultimaRuta))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_ultimaRuta}\"") { UseShellExecute = true });
            }
        };
        _salidaVista.DocumentoGenerado += AlGenerarVista;
        Disposed += (_, _) => _salidaVista.DocumentoGenerado -= AlGenerarVista;

        var salida = new Panel { Dock = DockStyle.Fill };
        salida.Controls.Add(_vista);
        _vista.Dock = DockStyle.Fill;
        salida.Controls.Add(_ruta);
        salida.Controls.Add(Tema.Fila(
            Tema.Etiqueta("Tipo:"),
            _tipo,
            Tema.Boton("&Vista previa", async (_, _) => await EmitirAsync(VistaPrevia), primario: true),
            Tema.Boton("&Exportar TXT", async (_, _) => await EmitirAsync(ArchivoTxt))));

        var izquierda = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        izquierda.Panel1.Controls.Add(Tema.Tarjeta("Vista previa del reporte (ventas brutas por día)", _grafico));
        izquierda.Panel2.Controls.Add(Tema.Tarjeta("Pedidos confirmados del periodo", _pedidos));

        var division = new SplitContainer { Dock = DockStyle.Fill };
        division.Panel1.Controls.Add(izquierda);
        division.Panel2.Controls.Add(Tema.Tarjeta("Salida / comprobante", salida));
        Contenido.Controls.Add(division);
        Contenido.Controls.Add(TarjetaKpi.Fila(_confirmados, _netas, _devoluciones));
        Contenido.Controls.Add(Tema.Fila(Tema.Etiqueta("&Desde:"), _desde, Tema.Etiqueta("&Hasta:"), _hasta, Tema.Boton("&Consultar", async (_, _) => await ActualizarAsync(), primario: true)));
        Resize += (_, _) =>
        {
            division.SplitterDistance = Math.Max(300, (int)(division.Width * 0.55));
            izquierda.SplitterDistance = Math.Max(120, (int)(izquierda.Height * 0.45));
        };
    }

    protected override async Task CargarAsync()
    {
        if (_hasta.Value.Date < _desde.Value.Date)
        {
            MessageBox.Show(this, "La fecha Hasta no puede ser anterior a Desde.", "Reportes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _hasta.Focus();
            return;
        }

        var (desde, hasta) = (_desde.Value.Date, _hasta.Value.Date);
        var (ok, r) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ConsultasOperativas>().ReporteAsync(desde, hasta));
        if (!ok || r is null)
        {
            return;
        }

        _confirmados.Valor = r.PedidosConfirmados.ToString(Tema.Cultura);
        _netas.Valor = Tema.Usd(r.VentasNetas);
        _devoluciones.Valor = $"{r.Devoluciones} ({Tema.Usd(r.Reembolsos)})";
        _grafico.Datos = r.Serie.Select(p => (p.Dia.ToString("dd/MM", Tema.Cultura), p.Ventas)).ToList();
        _pedidos.Mostrar(r.Pedidos);
    }

    private async Task EmitirAsync(string salida)
    {
        if (_pedidos.Seleccionado<PedidoResumenDto>() is not { } p)
        {
            MessageBox.Show(this, "Seleccione un pedido del periodo.", "Comprobantes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var tipo = (TipoComprobante)_tipo.SelectedItem!;
        var (ok, resultado) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioComprobantes>().EmitirAsync(p.Id, tipo, salida));
        if (!ok)
        {
            return;
        }

        if (salida == ArchivoTxt)
        {
            _ultimaRuta = resultado;
            _ruta.Text = $"Archivo generado: {resultado} (clic para abrir la carpeta)";
            _vista.Text = File.ReadAllText(resultado!);
        }
    }

    private void AlGenerarVista(object? sender, DocumentoVistaPrevia documento)
    {
        if (IsHandleCreated)
        {
            BeginInvoke(() =>
            {
                _vista.Text = documento.Contenido;
                _ruta.Text = $"Vista previa: {documento.Titulo}";
            });
        }
    }
}

/// <summary>Gráfico de barras simple dibujado con GDI+ (sin dependencias externas).</summary>
public sealed class GraficoBarras : Control
{
    private IReadOnlyList<(string Etiqueta, decimal Valor)> _datos = [];

    public GraficoBarras()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Tema.Superficie;
        Dock = DockStyle.Fill;
    }

    public IReadOnlyList<(string Etiqueta, decimal Valor)> Datos
    {
        get => _datos;
        set
        {
            _datos = value;
            AccessibleDescription = string.Join("; ", value.Select(d => $"{d.Etiqueta}: {Tema.Usd(d.Valor)}"));
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (_datos.Count == 0)
        {
            TextRenderer.DrawText(g, "Sin ventas en el periodo seleccionado.", Tema.Base, ClientRectangle, Tema.TextoSuave);
            return;
        }

        var area = new Rectangle(40, 12, Math.Max(10, Width - 56), Math.Max(10, Height - 44));
        var maximo = Math.Max(1m, _datos.Max(d => d.Valor));
        using var eje = new Pen(Tema.Borde);
        g.DrawLine(eje, area.Left, area.Bottom, area.Right, area.Bottom);
        var ancho = area.Width / (float)_datos.Count;
        using var barra = new SolidBrush(Tema.Primario);
        for (var i = 0; i < _datos.Count; i++)
        {
            var (etiqueta, valor) = _datos[i];
            var alto = (float)(valor / maximo) * (area.Height - 18);
            var x = area.Left + (i * ancho) + (ancho * 0.18f);
            var w = ancho * 0.64f;
            g.FillRectangle(barra, x, area.Bottom - alto, w, alto);
            var centro = new Rectangle((int)(area.Left + (i * ancho)), area.Bottom + 4, (int)ancho, 18);
            TextRenderer.DrawText(g, etiqueta, Tema.Base, centro, Tema.TextoSuave, TextFormatFlags.HorizontalCenter);
            if (valor > 0)
            {
                var texto = new Rectangle((int)(area.Left + (i * ancho)), (int)(area.Bottom - alto - 18), (int)ancho, 18);
                TextRenderer.DrawText(g, valor.ToString("0.00", Tema.Cultura), Tema.Base, texto, Tema.Texto, TextFormatFlags.HorizontalCenter);
            }
        }
    }
}
