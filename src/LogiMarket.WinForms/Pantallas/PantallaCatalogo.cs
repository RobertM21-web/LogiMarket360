using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Componentes;
using LogiMarket.WinForms.Dialogos;
using LogiMarket.WinForms.Mediador;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Pantallas;

/// <summary>Fila del catálogo: productos con stock propio y kits sin stock propio ("—").</summary>
public sealed record FilaCatalogo(string Tipo, int Id, string Codigo, string Nombre, string Categoria, decimal Precio, string Stock, string Estado);

/// <summary>Pantalla 03 — Catálogo y kits (Composite): búsqueda, altas, edición y estructura de kits.</summary>
public sealed class PantallaCatalogo : PantallaBase
{
    private readonly TextBox _buscar = new() { PlaceholderText = "SKU, nombre o categoría", Width = 280, AccessibleName = "Buscar en catálogo" };
    private readonly GrillaDatos _grilla = new() { AccessibleName = "Catálogo" };
    private readonly TreeView _arbol = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, AccessibleName = "Estructura del kit", Font = Tema.Base };
    private readonly Label _resumenKit = new() { AutoSize = true, ForeColor = Tema.TextoSuave, Dock = DockStyle.Bottom };
    private readonly Label _desglose = new() { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(0, 4, 0, 4), AccessibleName = "Desglose físico del kit" };
    private readonly Button _nuevoProducto;
    private readonly Button _nuevoKit;
    private readonly Button _editar;
    private readonly Button _activar;
    private readonly Button _composicion;
    private List<ProductoDto> _productos = [];
    private List<KitDto> _kits = [];
    private VistaCatalogo? _vista;

    public PantallaCatalogo(EjecutorOperaciones ejecutor, IMediadorPantallas mediador)
        : base(ClavesPantalla.Catalogo, "Catálogo y kits", "Productos simples y kits compuestos (un kit puede contener productos y otros kits).", ejecutor, mediador)
    {
        _grilla.Columna("Tipo", nameof(FilaCatalogo.Tipo), peso: 60)
            .Columna("SKU / Código", nameof(FilaCatalogo.Codigo), peso: 80)
            .Columna("Producto", nameof(FilaCatalogo.Nombre), peso: 140)
            .Columna("Categoría", nameof(FilaCatalogo.Categoria), peso: 90)
            .Columna("Precio", nameof(FilaCatalogo.Precio), FormatoColumna.Moneda, 70)
            .Columna("Stock", nameof(FilaCatalogo.Stock), FormatoColumna.Entero, 50)
            .Columna("Estado", nameof(FilaCatalogo.Estado), peso: 55);
        _grilla.ClaveDe = o => ((FilaCatalogo)o).Codigo;
        _grilla.SelectionChanged += (_, _) => MostrarEstructura();
        _grilla.CellDoubleClick += async (_, _) => await EditarAsync();
        _buscar.TextChanged += (_, _) => Filtrar();

        _nuevoProducto = Tema.Boton("Nuevo &producto", async (_, _) => await NuevoProductoAsync(), primario: true);
        _nuevoKit = Tema.Boton("Nuevo &kit", async (_, _) => await EditarKitAsync(null));
        _editar = Tema.Boton("&Editar", async (_, _) => await EditarAsync());
        _activar = Tema.Boton("Activar / desactivar", async (_, _) => await AlternarEstadoAsync());
        _composicion = Tema.Boton("Editar y guardar &composición", async (_, _) =>
        {
            if (_grilla.Seleccionado<FilaCatalogo>() is { Tipo: "KIT" } f)
            {
                await EditarKitAsync(_kits.First(k => k.Id == f.Id));
            }
        });

        var estructura = new Panel { Dock = DockStyle.Fill };
        estructura.Controls.Add(_arbol);
        estructura.Controls.Add(_desglose);
        estructura.Controls.Add(_resumenKit);
        estructura.Controls.Add(Tema.Fila(_composicion));

        var division = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 640, FixedPanel = FixedPanel.None };
        division.Panel1.Controls.Add(Tema.Tarjeta("Productos y kits", _grilla));
        division.Panel2.Controls.Add(Tema.Tarjeta("Estructura del kit", estructura));
        Contenido.Controls.Add(division);
        Contenido.Controls.Add(Tema.Fila(Tema.Etiqueta("&Buscar:"), _buscar, _nuevoProducto, _nuevoKit, _editar, _activar));
        Resize += (_, _) =>
        {
            division.SplitterDistance = Math.Max(200, (int)(division.Width * 0.66));
            _desglose.MaximumSize = _resumenKit.MaximumSize = new Size(Math.Max(120, division.Panel2.Width - 40), 0);
        };
    }

    protected override void AplicarPermisos()
    {
        var admin = Puede(Operacion.GestionarCatalogo);
        _nuevoProducto.Enabled = _nuevoKit.Enabled = _editar.Enabled = _activar.Enabled = _composicion.Enabled = admin;
    }

    protected override async Task CargarAsync()
    {
        var (ok, datos) = await Ejecutor.IntentarAsync(this, async sp =>
        {
            var c = sp.GetRequiredService<ConsultasOperativas>();
            return (await c.ProductosAsync(), await c.KitsAsync(), await sp.GetRequiredService<ConsultaCatalogoVisual>().CargarAsync());
        });
        if (!ok)
        {
            return;
        }

        (_productos, _kits, _vista) = (datos.Item1.ToList(), datos.Item2.ToList(), datos.Item3);
        Filtrar();
    }

    private void Filtrar()
    {
        var texto = _buscar.Text.Trim();
        var filas = (_vista?.Filas ?? [])
            .Select(f => new FilaCatalogo(f.Tipo, f.Id, f.Codigo, f.Nombre, f.Categoria, f.Precio, f.Stock?.ToString(Tema.Cultura) ?? "—", f.Activo ? "Activo" : "Inactivo"))
            .Where(f => texto.Length == 0
                || f.Codigo.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || f.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || f.Categoria.Contains(texto, StringComparison.OrdinalIgnoreCase));
        _grilla.Mostrar(filas);
        MostrarEstructura();
    }

    private void MostrarEstructura()
    {
        _arbol.BeginUpdate();
        _arbol.Nodes.Clear();
        var fila = _grilla.Seleccionado<FilaCatalogo>();
        _composicion.Visible = fila?.Tipo == "KIT";
        if (fila?.Tipo == "KIT" && _kits.FirstOrDefault(k => k.Id == fila.Id) is { } kit)
        {
            var raiz = _arbol.Nodes.Add($"{kit.Codigo} — {kit.Nombre}");
            Agregar(raiz.Nodes, kit, 0);
            _arbol.ExpandAll();
            MostrarResumenKit(kit.Codigo);
        }
        else if (fila is not null)
        {
            _arbol.Nodes.Add($"{fila.Codigo} — {fila.Nombre} (producto simple)");
            _resumenKit.Text = "Seleccione un kit para ver su estructura.";
            _desglose.Text = string.Empty;
        }

        _arbol.EndUpdate();
    }

    /// <summary>Muestra lo que calculó Application: precio (Composite), desglose y peso (Visitor).</summary>
    private void MostrarResumenKit(string codigo)
    {
        if (_vista?.Kits.GetValueOrDefault(codigo) is not { } e)
        {
            _resumenKit.Text = _desglose.Text = string.Empty;
            return;
        }

        var lineas = string.Join(", ", e.Desglose.Select(l => $"{l.Sku} ×{l.Cantidad}"));
        _desglose.Text = $"Desglose por SKU final: {lineas}{Environment.NewLine}Peso logístico: {e.PesoKg.ToString("0.00", Tema.Cultura)} kg · kits visitados: {string.Join(" → ", e.KitsVisitados)}";
        _resumenKit.Text = $"Precio derivado: {Tema.Usd(e.Precio)} (suma recursiva de componentes; sin stock propio)";
    }

    private void Agregar(TreeNodeCollection nodos, KitDto kit, int profundidad)
    {
        foreach (var c in kit.Componentes)
        {
            var nodo = nodos.Add($"{c.Codigo} × {c.Cantidad} — {c.Nombre}");
            if (c.KitHijoId is int hijoId && profundidad < 20 && _kits.FirstOrDefault(k => k.Id == hijoId) is { } hijo)
            {
                Agregar(nodo.Nodes, hijo, profundidad + 1);
            }
        }
    }

    private async Task NuevoProductoAsync()
    {
        using var dialogo = new DialogoProducto(null);
        if (dialogo.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var (ok, r) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioCatalogo>().CrearProductoAsync(dialogo.Datos));
        if (ok && EjecutorOperaciones.Informar(this, r!, "Nuevo producto"))
        {
            await NotificarAsync(EventoPantalla.CatalogoActualizado);
        }
    }

    private async Task EditarAsync()
    {
        var fila = _grilla.Seleccionado<FilaCatalogo>();
        if (fila is null || !Puede(Operacion.GestionarCatalogo))
        {
            return;
        }

        if (fila.Tipo == "KIT")
        {
            await EditarKitAsync(_kits.First(k => k.Id == fila.Id));
            return;
        }

        var p = _productos.First(x => x.Id == fila.Id);
        using var dialogo = new DialogoProducto(p);
        if (dialogo.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var (ok, r) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioCatalogo>().ActualizarProductoAsync(p.Id, dialogo.Datos));
        if (ok && EjecutorOperaciones.Informar(this, r!, "Editar producto"))
        {
            await NotificarAsync(EventoPantalla.CatalogoActualizado);
        }
    }

    private async Task AlternarEstadoAsync()
    {
        var fila = _grilla.Seleccionado<FilaCatalogo>();
        if (fila is null)
        {
            return;
        }

        if (fila.Tipo == "KIT")
        {
            MessageBox.Show(this, "Los kits se desactivan desactivando o retirando sus componentes.", "Catálogo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var activar = fila.Estado != "Activo";
        if (!EjecutorOperaciones.Confirmar(this, $"¿{(activar ? "Activar" : "Desactivar")} {fila.Codigo} — {fila.Nombre}?", "Catálogo"))
        {
            return;
        }

        var (ok, r) = await Ejecutor.IntentarAsync(this, sp => sp.GetRequiredService<ServicioCatalogo>().CambiarEstadoProductoAsync(fila.Id, activar));
        if (ok && EjecutorOperaciones.Informar(this, r!, "Catálogo"))
        {
            await NotificarAsync(EventoPantalla.CatalogoActualizado);
        }
    }

    private async Task EditarKitAsync(KitDto? kit)
    {
        using var dialogo = new DialogoKit(kit, _productos.Where(p => p.Activo).ToList(), _kits);
        if (dialogo.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var (ok, r) = await Ejecutor.IntentarAsync(this, sp => kit is null
            ? sp.GetRequiredService<ServicioCatalogo>().CrearKitAsync(dialogo.Codigo, dialogo.NombreKit, dialogo.Descripcion, dialogo.Composicion)
            : sp.GetRequiredService<ServicioCatalogo>().GuardarComposicionAsync(kit.Id, dialogo.Composicion));
        if (ok && EjecutorOperaciones.Informar(this, r!, kit is null ? "Nuevo kit" : "Guardar composición"))
        {
            await NotificarAsync(EventoPantalla.CatalogoActualizado);
        }
    }
}
