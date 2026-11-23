using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Catalogo.Composite;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Pedidos.Borrador;
using LogiMarket.Domain.Promociones;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Componentes;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>Línea mostrada del borrador.</summary>
public sealed record FilaBorrador(string Codigo, string Articulo, int Cantidad, decimal PrecioUnitario, decimal Importe);

/// <summary>
/// Pantalla 05 — Carrito y borrador. Cada edición pasa por <see cref="HistorialBorrador"/> (Memento, 5 estados),
/// la valoración usa Strategy + Abstract Factory y "Repetir último pedido" clona el borrador (Prototype).
/// </summary>
public sealed class PantallaCarrito : PantallaBase
{
    private sealed record OpcionCliente(Cliente Cliente)
    {
        public override string ToString() => $"{Cliente.Nombre} · {Cliente.Categoria.ToString().ToUpperInvariant()}";
    }

    private sealed record OpcionElemento(IElementoCatalogo Elemento)
    {
        public override string ToString() => $"{Elemento.Codigo} — {Elemento.Nombre} ({Tema.Usd(Elemento.Precio.Valor)})";
    }

    private readonly ComboBox _cliente = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360, AccessibleName = "Cliente" };
    private readonly ComboBox _entrega = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, AccessibleName = "Tipo de entrega" };
    private readonly ComboBox _producto = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360, AccessibleName = "Producto" };
    private readonly ComboBox _kit = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360, AccessibleName = "Kit" };
    private readonly NumericUpDown _cantidad = new() { Minimum = 1, Maximum = 999, Value = 1, Width = 70, AccessibleName = "Cantidad" };
    private readonly GrillaDatos _grilla = new() { AccessibleName = "Líneas del borrador" };
    private readonly Label _subtotal = Tema.Valor();
    private readonly Label _descuento = Tema.Valor();
    private readonly Label _envio = Tema.Valor();
    private readonly Label _total = new() { AutoSize = true, Font = Tema.Kpi, ForeColor = Tema.Primario, Text = "USD 0.00" };
    private readonly Label _historial = new() { AutoSize = true, ForeColor = Tema.TextoSuave, Dock = DockStyle.Bottom };
    private readonly ListBox _recorrido = new() { IntegralHeight = false, Height = 90, Dock = DockStyle.Bottom, AccessibleName = "Recorrido de la validación" };
    private readonly Button _anadirProducto;
    private readonly Button _anadirKit;
    private readonly Button _deshacer;
    private readonly Button _rehacer;
    private readonly Button _quitar;
    private readonly Button _validar;
    private readonly Button _repetir;
    private readonly CalculadoraPedido _calculadora;
    private readonly CatalogoFamiliasEntrega _familias;
    private PedidoBorrador _borrador = new();
    private HistorialBorrador _memento = new();
    private PedidoBorrador? _ultimoValidado;
    private bool _sincronizando;

    public PantallaCarrito(EjecutorOperaciones ejecutor, IMediadorPantallas mediador, CalculadoraPedido calculadora, CatalogoFamiliasEntrega familias)
        : base(ClavesPantalla.Carrito, "Carrito y borrador", "Arme el pedido; puede deshacer/rehacer hasta 5 ediciones antes de validar.", ejecutor, mediador)
    {
        _calculadora = calculadora;
        _familias = familias;
        _entrega.Items.AddRange([TipoEntrega.Estandar, TipoEntrega.Expres]);
        _entrega.SelectedIndex = 0;
        _grilla.Columna("Código", nameof(FilaBorrador.Codigo), peso: 60)
            .Columna("Artículo", nameof(FilaBorrador.Articulo), peso: 160)
            .Columna("Cantidad", nameof(FilaBorrador.Cantidad), FormatoColumna.Entero, 50)
            .Columna("P. unitario", nameof(FilaBorrador.PrecioUnitario), FormatoColumna.Moneda, 70)
            .Columna("Importe", nameof(FilaBorrador.Importe), FormatoColumna.Moneda, 70);
        _grilla.ClaveDe = o => ((FilaBorrador)o).Codigo;

        _cliente.SelectedIndexChanged += (_, _) =>
        {
            if (!_sincronizando && _cliente.SelectedItem is OpcionCliente o && o.Cliente.Id != _borrador.Cliente?.Id)
            {
                Editar($"Cliente {o.Cliente.Nombre}", b => b.SeleccionarCliente(o.Cliente));
            }
        };
        _entrega.SelectedIndexChanged += (_, _) =>
        {
            if (!_sincronizando && _entrega.SelectedItem is TipoEntrega t && t != _borrador.Entrega)
            {
                Editar($"Entrega {t}", b => b.CambiarEntrega(t));
            }
        };

        _anadirProducto = Tema.Boton("Añadir &producto", (_, _) => Anadir(_producto));
        _anadirKit = Tema.Boton("Añadir &kit", (_, _) => Anadir(_kit));
        _deshacer = Tema.Boton("&Deshacer", (_, _) => Accion(() => _memento.Deshacer(_borrador)));
        _rehacer = Tema.Boton("&Rehacer", (_, _) => Accion(() => _memento.Rehacer(_borrador)));
        _quitar = Tema.Boton("&Quitar línea", (_, _) =>
        {
            if (_grilla.Seleccionado<FilaBorrador>() is { } f)
            {
                Editar($"Quitar {f.Codigo}", b => b.QuitarLinea(f.Codigo));
            }
        });
        var cambiarCantidad = Tema.Boton("Cambiar ca&ntidad", (_, _) =>
        {
            if (_grilla.Seleccionado<FilaBorrador>() is { } f)
            {
                Editar($"Cantidad {f.Codigo}={_cantidad.Value}", b => b.CambiarCantidad(f.Codigo, (int)_cantidad.Value));
            }
        });
        _validar = Tema.Boton("&Validar pedido", async (_, _) => await ValidarAsync(), primario: true);
        _repetir = Tema.Boton("Repetir último pedido (Prototype)", (_, _) => RepetirUltimo());
        var nuevo = Tema.Boton("Nuevo borrador", (_, _) =>
        {
            if (_borrador.EstaVacio || EjecutorOperaciones.Confirmar(this, "¿Descartar el borrador actual? Esta acción no se puede deshacer.", "Nuevo borrador"))
            {
                Reiniciar(new PedidoBorrador());
            }
        });

        var seleccion = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
        seleccion.Controls.AddRange([Tema.Etiqueta("C&liente:"), _cliente, Tema.Etiqueta("&Entrega:"), _entrega]);
        seleccion.Controls.AddRange([Tema.Etiqueta("Producto:"), _producto, Tema.Etiqueta("Cantidad:"), _cantidad]);
        seleccion.Controls.Add(Tema.Etiqueta("Kit:"));
        seleccion.Controls.Add(_kit);
        seleccion.Controls.Add(_anadirProducto);
        seleccion.Controls.Add(_anadirKit);

        var lineas = new Panel { Dock = DockStyle.Fill };
        lineas.Controls.Add(_grilla);
        lineas.Controls.Add(_historial);
        lineas.Controls.Add(Tema.Fila(_deshacer, _rehacer, _quitar, cambiarCantidad, nuevo, _repetir));

        var resumen = new Panel { Dock = DockStyle.Fill };
        resumen.Controls.Add(_recorrido);
        resumen.Controls.Add(Tema.Fila(_validar));
        resumen.Controls.Add(_total);
        _total.Dock = DockStyle.Top;
        resumen.Controls.Add(Tema.Ficha(("Subtotal", _subtotal), ("Descuento", _descuento), ("Entrega", _envio)));

        var division = new SplitContainer { Dock = DockStyle.Fill };
        division.Panel1.Controls.Add(Tema.Tarjeta("Líneas", lineas));
        division.Panel2.Controls.Add(Tema.Tarjeta("Resumen", resumen));
        Contenido.Controls.Add(division);
        Contenido.Controls.Add(seleccion);
        Resize += (_, _) => division.SplitterDistance = Math.Max(300, (int)(division.Width * 0.64));
        Pintar();
    }

    protected override void AplicarPermisos()
    {
        var puede = Puede(Operacion.GestionarBorradores);
        foreach (var c in new Control[] { _cliente, _entrega, _producto, _kit, _cantidad, _anadirProducto, _anadirKit, _quitar, _validar, _repetir })
        {
            c.Enabled = puede;
        }

        Pintar();
    }

    protected override async Task CargarAsync()
    {
        var (ok, datos) = await Ejecutor.IntentarAsync(this, async sp =>
        {
            var s = sp.GetRequiredService<ServicioPedidos>();
            return (await s.ClientesActivosAsync(), await s.CargarCatalogoAsync());
        });
        if (!ok)
        {
            return;
        }

        var (clientes, arbol) = datos;
        _sincronizando = true;
        _cliente.Items.Clear();
        _cliente.Items.AddRange(clientes.Select(c => (object)new OpcionCliente(c)).ToArray());
        Rellenar(_producto, arbol.Elementos.Where(e => !e.EsKit));
        Rellenar(_kit, arbol.Elementos.Where(e => e.EsKit));
        _sincronizando = false;
        Pintar();
    }

    private static void Rellenar(ComboBox combo, IEnumerable<IElementoCatalogo> elementos)
    {
        var anterior = (combo.SelectedItem as OpcionElemento)?.Elemento.Codigo;
        combo.Items.Clear();
        combo.Items.AddRange(elementos.OrderBy(e => e.Codigo).Select(e => (object)new OpcionElemento(e)).ToArray());
        var indice = combo.Items.Cast<OpcionElemento>().ToList().FindIndex(o => o.Elemento.Codigo == anterior);
        combo.SelectedIndex = combo.Items.Count == 0 ? -1 : Math.Max(0, indice);
    }

    private void Anadir(ComboBox origen)
    {
        if (origen.SelectedItem is OpcionElemento o)
        {
            var cantidad = (int)_cantidad.Value;
            Editar($"Añadir {o.Elemento.Codigo} × {cantidad}", b => b.AgregarLinea(o.Elemento, cantidad));
        }
    }

    private void Editar(string descripcion, Action<PedidoBorrador> edicion) => Accion(() => _memento.Ejecutar(_borrador, descripcion, edicion));

    private void Accion(Action accion)
    {
        try
        {
            accion();
        }
        catch (Domain.Comun.ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "Borrador", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, ex.Message, "Borrador", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        Pintar();
    }

    private void Pintar()
    {
        _sincronizando = true;
        _cliente.SelectedItem = _cliente.Items.Cast<OpcionCliente>().FirstOrDefault(o => o.Cliente.Id == _borrador.Cliente?.Id);
        _entrega.SelectedItem = _borrador.Entrega;
        _sincronizando = false;

        _grilla.Mostrar(_borrador.Lineas.Select(l => new FilaBorrador(l.Elemento.Codigo, l.Elemento.Nombre + (l.Elemento.EsKit ? " (kit)" : string.Empty), l.Cantidad, l.Elemento.Precio.Valor, l.Elemento.Precio.Valor * l.Cantidad)));
        var resumen = _borrador.Valorar(_calculadora, _familias);
        _subtotal.Text = resumen is null ? "—" : Tema.Usd(resumen.Subtotal.Valor);
        _descuento.Text = resumen is null ? "—" : $"−{Tema.Usd(resumen.Descuento.Valor)} ({resumen.Politica} {resumen.Tasa:P0})";
        _envio.Text = resumen is null ? "—" : $"{Tema.Usd(resumen.Tarifa.Valor)} ({_borrador.Entrega.ToString().ToUpperInvariant()})";
        _total.Text = "TOTAL " + Tema.Usd(resumen?.Total.Valor ?? 0);

        var editable = Puede(Operacion.GestionarBorradores);
        _deshacer.Enabled = editable && _memento.PuedeDeshacer;
        _rehacer.Enabled = editable && _memento.PuedeRehacer;
        _quitar.Enabled = editable && !_borrador.EstaVacio;
        _validar.Enabled = editable && resumen is not null;
        _repetir.Enabled = editable && _ultimoValidado is not null;
        _historial.Text = $"Historial (Memento): {_memento.EstadosGuardados}/{HistorialBorrador.Capacidad} estados para deshacer · rehacer {(_memento.PuedeRehacer ? "disponible" : "—")}";
    }

    private async Task ValidarAsync()
    {
        var copia = _borrador.Clonar();
        var (ok, r) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioPedidos>().GuardarYValidarAsync(_borrador));
        if (!ok || r is null)
        {
            return;
        }

        _recorrido.Items.Clear();
        _recorrido.Items.AddRange(r.Recorrido.Select(p => (object)p).ToArray());
        if (!r.Exito)
        {
            MessageBox.Show(this, $"{r.Mensaje}\n\nCódigo: {r.Codigo}\nEl borrador se conserva; puede corregirlo o deshacer.", "Validación rechazada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _memento.LimpiarYBloquear();
        _ultimoValidado = copia;
        MessageBox.Show(this, $"Pedido {r.CodigoPedido} VALIDADO. Pase a cobro para confirmarlo.", "Validar pedido", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Reiniciar(new PedidoBorrador());
        await NotificarAsync(EventoPantalla.PedidoValidado, r.PedidoId);
    }

    private void RepetirUltimo()
    {
        if (_ultimoValidado is null || (!_borrador.EstaVacio && !EjecutorOperaciones.Confirmar(this, "¿Reemplazar el borrador actual por una copia del último pedido validado?", "Repetir pedido")))
        {
            return;
        }

        Reiniciar(_ultimoValidado.Clonar());
    }

    private void Reiniciar(PedidoBorrador borrador)
    {
        _borrador = borrador;
        _memento = new HistorialBorrador();
        _recorrido.Items.Clear();
        Pintar();
    }
}
