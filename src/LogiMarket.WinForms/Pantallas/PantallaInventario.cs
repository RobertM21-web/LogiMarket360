using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Componentes;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>Existencias por SKU con alerta de stock bajo.</summary>
public sealed record FilaExistencia(string Sku, string Producto, int Disponible, string Alerta);

/// <summary>Movimiento mostrado con signo y referencia (p. ej. PED-001 P01 −1).</summary>
public sealed record FilaMovimiento(DateTime FechaUtc, string Referencia, string Sku, string Cantidad, string Motivo, int StockResultante, string Usuario);

/// <summary>Pantalla 10 — Inventario y movimientos (ajustes auditados, solo ADMIN).</summary>
public sealed class PantallaInventario : PantallaBase
{
    private readonly GrillaDatos _existencias = new() { AccessibleName = "Existencias" };
    private readonly GrillaDatos _movimientos = new() { AccessibleName = "Movimientos recientes" };
    private readonly ComboBox _sku = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _cantidad = new() { Minimum = -1000, Maximum = 1000, Value = 1 };
    private readonly TextBox _motivo = new() { PlaceholderText = "Motivo obligatorio (p. ej. conteo físico)" };
    private readonly ErrorProvider _errores = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private readonly Button _registrar;

    public PantallaInventario(EjecutorOperaciones ejecutor, IMediadorPantallas mediador)
        : base(ClavesPantalla.Inventario, "Inventario y movimientos", $"El stock solo cambia por movimientos registrados. Alerta BAJO cuando quedan ≤ {ConsultasOperativas.UmbralStockBajo} unidades.", ejecutor, mediador)
    {
        _existencias.Columna("SKU", nameof(FilaExistencia.Sku), peso: 50)
            .Columna("Producto", nameof(FilaExistencia.Producto), peso: 130)
            .Columna("Disponible", nameof(FilaExistencia.Disponible), FormatoColumna.Entero, 60)
            .Columna("Alerta", nameof(FilaExistencia.Alerta), peso: 45);
        _existencias.ClaveDe = o => ((FilaExistencia)o).Sku;
        _existencias.CellFormatting += (_, e) =>
        {
            if (_existencias.Columns[e.ColumnIndex].Name == nameof(FilaExistencia.Alerta) && e.Value is "BAJO")
            {
                e.CellStyle!.ForeColor = Tema.Alerta;
                e.CellStyle.Font = Tema.Negrita;
            }
        };
        _existencias.SelectionChanged += (_, _) =>
        {
            if (_existencias.Seleccionado<FilaExistencia>() is { } f)
            {
                _sku.SelectedItem = f.Sku;
            }
        };
        _movimientos.Columna("Fecha", nameof(FilaMovimiento.FechaUtc), FormatoColumna.Fecha, 80)
            .Columna("Referencia", nameof(FilaMovimiento.Referencia), peso: 70)
            .Columna("SKU", nameof(FilaMovimiento.Sku), peso: 40)
            .Columna("Cantidad", nameof(FilaMovimiento.Cantidad), FormatoColumna.Entero, 45)
            .Columna("Motivo", nameof(FilaMovimiento.Motivo), peso: 70)
            .Columna("Stock result.", nameof(FilaMovimiento.StockResultante), FormatoColumna.Entero, 50)
            .Columna("Usuario", nameof(FilaMovimiento.Usuario), peso: 60);

        _registrar = Tema.Boton("&Registrar ajuste", async (_, _) => await RegistrarAsync(), primario: true);
        var nuevo = new Panel { Dock = DockStyle.Fill };
        nuevo.Controls.Add(Tema.Fila(_registrar));
        nuevo.Controls.Add(Tema.Ficha(("SKU", _sku), ("Cantidad (+/−)", _cantidad), ("Motivo", _motivo)));

        var arriba = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        arriba.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        arriba.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        arriba.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        arriba.Controls.Add(Tema.Tarjeta("Existencias", _existencias), 0, 0);
        arriba.Controls.Add(Tema.Tarjeta("Movimiento nuevo (auditado)", nuevo), 1, 0);

        var division = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        division.Panel1.Controls.Add(arriba);
        division.Panel2.Controls.Add(Tema.Tarjeta("Movimientos recientes", _movimientos));
        Contenido.Controls.Add(division);
        Resize += (_, _) => division.SplitterDistance = Math.Max(160, (int)(division.Height * 0.48));
    }

    protected override void AplicarPermisos() =>
        _registrar.Enabled = _sku.Enabled = _cantidad.Enabled = _motivo.Enabled = Puede(Operacion.AjustarInventario);

    protected override async Task CargarAsync()
    {
        var (ok, datos) = await Ejecutor.IntentarAsync(this, async sp =>
        {
            var c = sp.GetRequiredService<ConsultasOperativas>();
            return (await c.ProductosAsync(), await c.MovimientosAsync());
        });
        if (!ok)
        {
            return;
        }

        var (productos, movimientos) = datos;
        _existencias.Mostrar(productos.OrderBy(p => p.Sku).Select(p => new FilaExistencia(p.Sku, p.Nombre, p.Stock, p.Stock <= ConsultasOperativas.UmbralStockBajo ? "BAJO" : "OK")));
        _movimientos.Mostrar(movimientos.Select(m => new FilaMovimiento(m.FechaUtc, m.Referencia, m.Sku, m.Cantidad > 0 ? $"+{m.Cantidad}" : $"−{-m.Cantidad}", m.Motivo, m.StockResultante, m.Usuario)));
        var anterior = _sku.SelectedItem;
        _sku.Items.Clear();
        _sku.Items.AddRange(productos.OrderBy(p => p.Sku).Select(p => (object)p.Sku).ToArray());
        _sku.SelectedItem = anterior ?? _sku.Items.Cast<object>().FirstOrDefault();
    }

    private async Task RegistrarAsync()
    {
        _errores.Clear();
        if (_sku.SelectedItem is not string sku)
        {
            _errores.SetError(_sku, "Seleccione un SKU.");
            return;
        }

        if (_cantidad.Value == 0)
        {
            _errores.SetError(_cantidad, "La cantidad no puede ser cero.");
            _cantidad.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_motivo.Text))
        {
            _errores.SetError(_motivo, "Indique el motivo del ajuste.");
            _motivo.Focus();
            return;
        }

        var cantidad = (int)_cantidad.Value;
        if (!EjecutorOperaciones.Confirmar(this, $"¿Registrar ajuste {(cantidad > 0 ? "+" : string.Empty)}{cantidad} en {sku}?\nMotivo: {_motivo.Text.Trim()}", "Ajuste de inventario"))
        {
            return;
        }

        var motivo = _motivo.Text.Trim();
        var (ok, r) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioInventario>().AjustarAsync(sku, cantidad, motivo));
        if (ok && EjecutorOperaciones.Informar(this, r!, "Ajuste de inventario"))
        {
            _motivo.Clear();
            await NotificarAsync(EventoPantalla.InventarioActualizado);
        }
    }
}
