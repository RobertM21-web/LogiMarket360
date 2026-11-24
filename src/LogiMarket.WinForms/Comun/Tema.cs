using System.Globalization;

namespace LogiMarket.WinForms.Comun;

/// <summary>Paleta, tipografía y fábricas de controles con estilo y accesibilidad coherentes.</summary>
public static class Tema
{
    public static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

    public static readonly Color Fondo = Color.FromArgb(244, 246, 249);
    public static readonly Color Superficie = Color.White;
    public static readonly Color Lateral = Color.FromArgb(24, 39, 64);
    public static readonly Color LateralActivo = Color.FromArgb(44, 98, 168);
    public static readonly Color Primario = Color.FromArgb(32, 92, 168);
    public static readonly Color Texto = Color.FromArgb(28, 33, 41);
    public static readonly Color TextoSuave = Color.FromArgb(96, 106, 120);
    public static readonly Color Borde = Color.FromArgb(214, 220, 228);
    public static readonly Color Exito = Color.FromArgb(22, 128, 61);
    public static readonly Color Alerta = Color.FromArgb(180, 83, 9);
    public static readonly Color Peligro = Color.FromArgb(185, 28, 28);

    public static readonly Font Base = new("Segoe UI", 9.75f);
    public static readonly Font Negrita = new("Segoe UI Semibold", 9.75f);
    public static readonly Font Titulo = new("Segoe UI Semibold", 15f);
    public static readonly Font Subtitulo = new("Segoe UI Semibold", 11f);
    public static readonly Font Kpi = new("Segoe UI Semibold", 20f);
    public static readonly Font Monoespaciada = new("Consolas", 9.75f);

    public static string Usd(decimal valor) => "USD " + valor.ToString("0.00", Cultura);

    public static string Fecha(DateTime? utc) => utc is null ? "—" : utc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura);

    public static Button Boton(string texto, EventHandler alPulsar, bool primario = false, string? accesible = null)
    {
        var b = new Button
        {
            Text = texto,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 8, 8),
            FlatStyle = FlatStyle.Flat,
            Font = primario ? Negrita : Base,
            BackColor = primario ? Primario : Superficie,
            ForeColor = primario ? Color.White : Texto,
            Cursor = Cursors.Hand,
            AccessibleName = accesible ?? texto.Replace("&", string.Empty),
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderColor = primario ? Primario : Borde;
        b.EnabledChanged += (_, _) =>
        {
            // Estado deshabilitado visible también en botones planos (no basta el color de texto).
            b.BackColor = !b.Enabled ? Color.FromArgb(226, 230, 236) : primario ? Primario : Superficie;
            b.ForeColor = !b.Enabled ? TextoSuave : primario ? Color.White : Texto;
            b.FlatAppearance.BorderColor = !b.Enabled ? Borde : primario ? Primario : Borde;
        };
        b.Click += alPulsar;
        return b;
    }

    public static Label Etiqueta(string texto, Font? fuente = null, Color? color = null) => new()
    {
        Text = texto,
        AutoSize = true,
        Font = fuente ?? Base,
        ForeColor = color ?? Texto,
        Margin = new Padding(0, 6, 8, 4),
    };

    public static GroupBox Tarjeta(string titulo, Control contenido)
    {
        var g = new GroupBox
        {
            Text = titulo,
            Dock = DockStyle.Fill,
            Font = Negrita,
            ForeColor = Texto,
            BackColor = Superficie,
            Padding = new Padding(10, 8, 10, 10),
            Margin = new Padding(6),
        };
        contenido.Dock = DockStyle.Fill;
        contenido.Font = Base;
        g.Controls.Add(contenido);
        return g;
    }

    public static FlowLayoutPanel Fila(params Control[] controles)
    {
        var f = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Padding = new Padding(0, 2, 0, 2) };
        f.Controls.AddRange(controles);
        return f;
    }

    /// <summary>Tabla de dos columnas Etiqueta: valor (fichas de detalle).</summary>
    public static TableLayoutPanel Ficha(params (string Etiqueta, Control Valor)[] filas)
    {
        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 4, 0, 4) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var (etiqueta, valor) in filas)
        {
            var e = Etiqueta(etiqueta, color: TextoSuave);
            valor.Margin = new Padding(0, 3, 0, 3);
            if (valor is TextBox or ComboBox or NumericUpDown)
            {
                valor.Dock = DockStyle.Fill;
                e.Text += ":";
                valor.AccessibleName = etiqueta;
            }

            t.Controls.Add(e);
            t.Controls.Add(valor);
        }

        return t;
    }

    public static Label Valor(string texto = "—", bool negrita = false) => new()
    {
        Text = texto,
        AutoSize = true,
        Font = negrita ? Negrita : Base,
        ForeColor = Texto,
    };

    /// <summary>Chip de estado con color semántico.</summary>
    public static Label Chip(string texto, Color color) => new()
    {
        Text = " " + texto + " ",
        AutoSize = true,
        Font = Negrita,
        ForeColor = Color.White,
        BackColor = color,
        Padding = new Padding(4, 2, 4, 2),
        Margin = new Padding(0, 4, 6, 4),
    };

    public static Color ColorEstado(string estado) => estado switch
    {
        "BORRADOR" => Color.FromArgb(100, 116, 139),
        "VALIDADO" => Color.FromArgb(2, 132, 199),
        "PAGADO" => Exito,
        "DESPACHADO" => Color.FromArgb(124, 58, 237),
        "ENTREGADO" => Color.FromArgb(15, 118, 110),
        "CANCELADO" => Peligro,
        _ => TextoSuave,
    };
}
