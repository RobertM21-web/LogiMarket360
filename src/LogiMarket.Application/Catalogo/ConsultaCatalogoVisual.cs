using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Catalogo.Visitantes;

namespace LogiMarket.Application.Catalogo;

/// <summary>
/// Renglón de la grilla de catálogo: referencia a los metadatos compartidos del SKU (Flyweight, estado
/// intrínseco) más el estado extrínseco propio de la fila.
/// </summary>
public sealed record FilaCatalogoVista(string Tipo, int Id, MetadatosSku Metadatos, decimal Precio, int? Stock, bool Activo)
{
    public string Codigo => Metadatos.Sku;

    public string Nombre => Metadatos.Nombre;

    public string Categoria => Metadatos.Categoria;
}

/// <summary>Resultado de visitar un kit: precio del Composite, desglose físico y peso logístico (Visitor).</summary>
public sealed record EstructuraKitVista(
    string Codigo,
    decimal Precio,
    decimal PesoKg,
    IReadOnlyList<LineaDesglose> Desglose,
    IReadOnlyList<string> KitsVisitados);

public sealed record VistaCatalogo(
    IReadOnlyList<FilaCatalogoVista> Filas,
    IReadOnlyDictionary<string, EstructuraKitVista> Kits,
    int MetadatosCompartidos);

/// <summary>
/// Consulta de la pantalla Catálogo y kits. Construye el Composite del catálogo, obtiene el precio de cada
/// kit de forma recursiva, recorre cada kit con <see cref="DesgloseSkuVisitor"/> y <see cref="PesoLogisticoVisitor"/>
/// y arma los renglones con metadatos compartidos por <see cref="CatalogoVisualFlyweightFactory"/>.
/// Ningún cálculo de precio, peso o desglose queda en el formulario.
/// </summary>
public sealed class ConsultaCatalogoVisual
{
    private readonly IAutorizador _autorizador;
    private readonly IRepositorioProductos _productos;
    private readonly IRepositorioKits _kits;
    private readonly CatalogoVisualFlyweightFactory _flyweights;

    public ConsultaCatalogoVisual(IAutorizador autorizador, IRepositorioProductos productos, IRepositorioKits kits, CatalogoVisualFlyweightFactory flyweights)
    {
        _autorizador = autorizador;
        _productos = productos;
        _kits = kits;
        _flyweights = flyweights;
    }

    public async Task<VistaCatalogo> CargarAsync(CancellationToken ct = default)
    {
        await _autorizador.VerificarAsync(Operacion.Consultar, "Vista de catálogo", ct);
        var productos = await _productos.ListarAsync(ct);
        var kits = await _kits.ListarAsync(ct);
        var arbol = ArbolCatalogo.Construir(productos, kits);

        var filas = new List<FilaCatalogoVista>(productos.Count + kits.Count);
        foreach (var p in productos.OrderBy(p => p.Sku, StringComparer.OrdinalIgnoreCase))
        {
            filas.Add(new FilaCatalogoVista("PRODUCTO", p.Id, _flyweights.Obtener(p.Sku, p.Nombre, p.Categoria, p.Descripcion), p.Precio.Valor, p.Stock, p.Activo));
        }

        var estructuras = new Dictionary<string, EstructuraKitVista>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in kits.OrderBy(k => k.Codigo, StringComparer.OrdinalIgnoreCase))
        {
            var elemento = arbol.Obtener(k.Codigo);
            var desglose = new DesgloseSkuVisitor();
            var peso = new PesoLogisticoVisitor();
            elemento.Aceptar(desglose);
            elemento.Aceptar(peso);
            estructuras[k.Codigo] = new EstructuraKitVista(k.Codigo, elemento.Precio.Valor, peso.TotalKg, desglose.Lineas.ToList(), desglose.KitsVisitados.ToList());
            filas.Add(new FilaCatalogoVista("KIT", k.Id, _flyweights.Obtener(k.Codigo, k.Nombre, "Kit", k.Descripcion), elemento.Precio.Valor, null, k.Activo));
        }

        return new VistaCatalogo(filas, estructuras, _flyweights.InstanciasCreadas);
    }
}
