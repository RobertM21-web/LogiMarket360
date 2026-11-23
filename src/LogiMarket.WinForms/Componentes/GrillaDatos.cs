using System.ComponentModel;
using LogiMarket.WinForms.Comun;

namespace LogiMarket.WinForms.Componentes;

public enum FormatoColumna
{
    Texto,
    Moneda,
    Entero,
    Fecha,
    Decimal,
}

/// <summary>
/// DataGridView reutilizable: columnas identificadas, formato decimal consistente (USD 0.00, cultura
/// invariante), fechas en hora local, solo lectura y selección persistente entre recargas.
/// </summary>
public sealed class GrillaDatos : DataGridView
{
    public GrillaDatos()
    {
        Dock = DockStyle.Fill;
        ReadOnly = true;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        AutoGenerateColumns = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = false;
        RowHeadersVisible = false;
        BackgroundColor = Tema.Superficie;
        BorderStyle = BorderStyle.None;
        GridColor = Tema.Borde;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        EnableHeadersVisualStyles = false;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(232, 237, 244),
            ForeColor = Tema.Texto,
            Font = Tema.Negrita,
            Padding = new Padding(4),
            SelectionBackColor = Color.FromArgb(232, 237, 244),
        };
        DefaultCellStyle = new DataGridViewCellStyle
        {
            Font = Tema.Base,
            ForeColor = Tema.Texto,
            SelectionBackColor = Color.FromArgb(207, 226, 250),
            SelectionForeColor = Tema.Texto,
            Padding = new Padding(4, 2, 4, 2),
        };
        RowTemplate.Height = 28;
        StandardTab = true; // TAB sale de la grilla hacia el siguiente control.
    }

    /// <summary>Obtiene la clave de una fila para conservar la selección al recargar.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, object?>? ClaveDe { get; set; }

    public GrillaDatos Columna(string titulo, string propiedad, FormatoColumna formato = FormatoColumna.Texto, float peso = 100)
    {
        var c = new DataGridViewTextBoxColumn
        {
            HeaderText = titulo,
            DataPropertyName = propiedad,
            Name = propiedad,
            FillWeight = peso,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        c.DefaultCellStyle.FormatProvider = Tema.Cultura;
        // Ancho mínimo legible: el encabezado completo y, según el formato, el valor típico; si no cabe, aparece desplazamiento horizontal.
        var minimo = TextRenderer.MeasureText(titulo, Tema.Negrita).Width + 28;
        c.MinimumWidth = Math.Max(minimo, formato switch
        {
            FormatoColumna.Fecha => TextRenderer.MeasureText("00/00/0000 00:00", Tema.Base).Width + 16,
            FormatoColumna.Moneda => TextRenderer.MeasureText("USD 0000.00", Tema.Base).Width + 16,
            _ => 40,
        });
        switch (formato)
        {
            case FormatoColumna.Moneda:
                c.DefaultCellStyle.Format = "'USD '0.00";
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                break;
            case FormatoColumna.Decimal:
                c.DefaultCellStyle.Format = "0.00";
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                break;
            case FormatoColumna.Entero:
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                break;
        }

        Columns.Add(c);
        return this;
    }

    public void Mostrar<T>(IEnumerable<T> filas)
    {
        var clave = CurrentRow?.DataBoundItem is { } actual ? ClaveDe?.Invoke(actual) : null;
        var lista = filas.ToList();
        DataSource = new BindingList<T>(lista);
        if (clave is null)
        {
            return;
        }

        foreach (DataGridViewRow fila in Rows)
        {
            if (fila.DataBoundItem is { } item && Equals(ClaveDe!(item), clave))
            {
                CurrentCell = fila.Cells.Cast<DataGridViewCell>().First(c => c.Visible);
                break;
            }
        }
    }

    public T? Seleccionado<T>()
        where T : class => CurrentRow?.DataBoundItem as T;

    protected override void OnCellFormatting(DataGridViewCellFormattingEventArgs e)
    {
        if (e.Value is DateTime fecha)
        {
            e.Value = Tema.Fecha(fecha.Kind == DateTimeKind.Local ? fecha.ToUniversalTime() : DateTime.SpecifyKind(fecha, DateTimeKind.Utc));
            e.FormattingApplied = true;
        }

        base.OnCellFormatting(e);
    }
}

/// <summary>Tarjeta de indicador (KPI) con título y valor.</summary>
public sealed class TarjetaKpi : Panel
{
    private readonly Label _valor;

    public TarjetaKpi(string titulo)
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Margin = new Padding(6);
        Padding = new Padding(14, 10, 14, 10);
        BorderStyle = BorderStyle.FixedSingle;
        _valor = new Label { Text = "—", Font = Tema.Kpi, ForeColor = Tema.Primario, AutoSize = true, Dock = DockStyle.Top };
        Controls.Add(_valor);
        Controls.Add(new Label { Text = titulo, ForeColor = Tema.TextoSuave, AutoSize = true, Dock = DockStyle.Top });
        AccessibleName = titulo;
    }

    public string Valor
    {
        get => _valor.Text;
        set
        {
            _valor.Text = value;
            AccessibleDescription = value;
        }
    }

    public Color ColorValor
    {
        get => _valor.ForeColor;
        set => _valor.ForeColor = value;
    }

    /// <summary>Fila de tarjetas KPI de igual ancho que escala con la ventana.</summary>
    public static TableLayoutPanel Fila(params TarjetaKpi[] tarjetas)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Top, Height = 124, ColumnCount = tarjetas.Length, RowCount = 1 };
        foreach (var k in tarjetas)
        {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / tarjetas.Length));
            t.Controls.Add(k);
        }

        return t;
    }
}
