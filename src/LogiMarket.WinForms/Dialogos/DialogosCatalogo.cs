using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Consultas;
using LogiMarket.Domain.Catalogo;
using LogiMarket.WinForms.Comun;

namespace LogiMarket.WinForms.Dialogos;

/// <summary>Base de diálogos modales con botones Aceptar/Cancelar y validación cercana al campo.</summary>
public abstract class DialogoBase : Form
{
    private readonly ErrorProvider _errores = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    protected DialogoBase(string titulo, Size tamano)
    {
        Text = titulo;
        Font = Tema.Base;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = tamano;
        MinimumSize = new Size(tamano.Width, Math.Min(tamano.Height, 320));
        BackColor = Tema.Superficie;
        Padding = new Padding(14);

        var aceptar = Tema.Boton("&Aceptar", (_, _) =>
        {
            _errores.Clear();
            if (Validar())
            {
                DialogResult = DialogResult.OK;
            }
        }, primario: true);
        var cancelar = Tema.Boton("&Cancelar", (_, _) => DialogResult = DialogResult.Cancel);
        AcceptButton = aceptar;
        CancelButton = cancelar;
        var botones = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
        botones.Controls.AddRange([cancelar, aceptar]);
        Controls.Add(botones);
    }

    protected abstract bool Validar();

    protected bool Error(Control campo, string mensaje)
    {
        _errores.SetError(campo, mensaje);
        campo.Focus();
        return false;
    }
}

/// <summary>Alta o edición de producto.</summary>
public sealed class DialogoProducto : DialogoBase
{
    private readonly TextBox _sku = new();
    private readonly TextBox _nombre = new();
    private readonly TextBox _categoria = new();
    private readonly TextBox _descripcion = new() { Multiline = true, Height = 60 };
    private readonly NumericUpDown _precio = new() { DecimalPlaces = 2, Maximum = 1_000_000, Increment = 0.5m };
    private readonly NumericUpDown _peso = new() { DecimalPlaces = 2, Maximum = 1_000, Increment = 0.05m };
    private readonly NumericUpDown _stock = new() { Maximum = 100_000 };

    public DialogoProducto(ProductoDto? producto)
        : base(producto is null ? "Nuevo producto" : $"Editar {producto.Sku}", new Size(460, 380))
    {
        var ficha = Tema.Ficha(("SKU", _sku), ("Nombre", _nombre), ("Categoría", _categoria), ("Descripción", _descripcion), ("Precio (USD)", _precio), ("Peso (kg)", _peso), ("Stock inicial", _stock));
        Controls.Add(ficha);
        ficha.BringToFront();
        if (producto is not null)
        {
            _sku.Text = producto.Sku;
            _sku.ReadOnly = true;
            _nombre.Text = producto.Nombre;
            _categoria.Text = producto.Categoria;
            _descripcion.Text = producto.Descripcion;
            _precio.Value = producto.Precio;
            _peso.Value = producto.PesoKg;
            _stock.Value = producto.Stock;
            _stock.Enabled = false; // El stock solo cambia por movimientos auditados (Inventario).
        }
    }

    public DatosProducto Datos => new(_sku.Text.Trim().ToUpperInvariant(), _nombre.Text.Trim(), _categoria.Text.Trim(), _descripcion.Text.Trim(), _precio.Value, _peso.Value, (int)_stock.Value);

    protected override bool Validar()
    {
        if (string.IsNullOrWhiteSpace(_sku.Text))
        {
            return Error(_sku, "El SKU es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(_nombre.Text))
        {
            return Error(_nombre, "El nombre es obligatorio.");
        }

        return _precio.Value <= 0 ? Error(_precio, "El precio debe ser mayor que cero.") : true;
    }
}

/// <summary>Alta de kit o edición de su composición (productos y kits hijos con cantidades).</summary>
public sealed class DialogoKit : DialogoBase
{
    private sealed record Opcion(string Texto, int? ProductoId, int? KitId)
    {
        public override string ToString() => Texto;
    }

    private sealed class LineaComposicion
    {
        public required Opcion Elemento { get; init; }

        public int Cantidad { get; set; }

        public string Componente => Elemento.Texto;
    }

    private readonly TextBox _codigo = new();
    private readonly TextBox _nombre = new();
    private readonly TextBox _descripcion = new();
    private readonly ComboBox _elemento = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, AccessibleName = "Componente" };
    private readonly NumericUpDown _cantidad = new() { Minimum = 1, Maximum = 1000, Value = 1, Width = 70, AccessibleName = "Cantidad" };
    private readonly ListBox _lineas = new() { IntegralHeight = false, AccessibleName = "Componentes del kit" };
    private readonly List<LineaComposicion> _composicion = new();

    public DialogoKit(KitDto? kit, IReadOnlyList<ProductoDto> productos, IReadOnlyList<KitDto> kits)
        : base(kit is null ? "Nuevo kit" : $"Composición de {kit.Codigo}", new Size(560, 480))
    {
        var opciones = productos.Select(p => new Opcion($"{p.Sku} — {p.Nombre}", p.Id, null))
            .Concat(kits.Where(k => k.Id != kit?.Id).Select(k => new Opcion($"{k.Codigo} — {k.Nombre} (kit)", null, k.Id)))
            .ToList();
        _elemento.Items.AddRange(opciones.Cast<object>().ToArray());
        if (_elemento.Items.Count > 0)
        {
            _elemento.SelectedIndex = 0;
        }

        var ficha = Tema.Ficha(("Código", _codigo), ("Nombre", _nombre), ("Descripción", _descripcion));
        var agregar = Tema.Boton("A&gregar", (_, _) => Agregar());
        var quitar = Tema.Boton("&Quitar", (_, _) =>
        {
            if (_lineas.SelectedIndex >= 0)
            {
                _composicion.RemoveAt(_lineas.SelectedIndex);
                Pintar();
            }
        });
        var aviso = Tema.Etiqueta("La composición se valida en el dominio: se rechazan ciclos (un kit no puede contenerse a sí mismo).", color: Tema.TextoSuave);
        aviso.MaximumSize = new Size(520, 0);
        var cuerpo = new Panel { Dock = DockStyle.Fill };
        cuerpo.Controls.Add(_lineas);
        cuerpo.Controls.Add(Tema.Fila(_elemento, _cantidad, agregar, quitar));
        cuerpo.Controls.Add(aviso);
        cuerpo.Controls.Add(ficha);
        Controls.Add(cuerpo);
        cuerpo.BringToFront();

        if (kit is not null)
        {
            _codigo.Text = kit.Codigo;
            _nombre.Text = kit.Nombre;
            _descripcion.Text = kit.Descripcion;
            _codigo.ReadOnly = _nombre.ReadOnly = _descripcion.ReadOnly = true;
            foreach (var c in kit.Componentes)
            {
                var texto = c.KitHijoId is null ? $"{c.Codigo} — {c.Nombre}" : $"{c.Codigo} — {c.Nombre} (kit)";
                _composicion.Add(new LineaComposicion { Elemento = new Opcion(texto, c.ProductoId, c.KitHijoId), Cantidad = c.Cantidad });
            }

            Pintar();
        }
    }

    public string Codigo => _codigo.Text.Trim().ToUpperInvariant();

    public string NombreKit => _nombre.Text.Trim();

    public string Descripcion => _descripcion.Text.Trim();

    public IReadOnlyList<ComposicionPropuesta> Composicion => _composicion
        .Select(l => l.Elemento.ProductoId is int p ? ComposicionPropuesta.DeProducto(p, l.Cantidad) : ComposicionPropuesta.DeKit(l.Elemento.KitId!.Value, l.Cantidad))
        .ToList();

    protected override bool Validar()
    {
        if (string.IsNullOrWhiteSpace(_codigo.Text))
        {
            return Error(_codigo, "El código del kit es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(_nombre.Text))
        {
            return Error(_nombre, "El nombre es obligatorio.");
        }

        return _composicion.Count == 0 ? Error(_lineas, "Agregue al menos un componente.") : true;
    }

    private void Agregar()
    {
        if (_elemento.SelectedItem is not Opcion opcion)
        {
            return;
        }

        var existente = _composicion.FirstOrDefault(l => l.Elemento.ProductoId == opcion.ProductoId && l.Elemento.KitId == opcion.KitId);
        if (existente is not null)
        {
            existente.Cantidad += (int)_cantidad.Value;
        }
        else
        {
            _composicion.Add(new LineaComposicion { Elemento = opcion, Cantidad = (int)_cantidad.Value });
        }

        Pintar();
    }

    private void Pintar()
    {
        _lineas.Items.Clear();
        _lineas.Items.AddRange(_composicion.Select(l => (object)$"{l.Componente} × {l.Cantidad}").ToArray());
    }
}
