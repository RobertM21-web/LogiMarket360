using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Catalogo.Composite;
using LogiMarket.Domain.Inventario;

namespace LogiMarket.Domain.Catalogo;

/// <summary>Producto físico del catálogo con existencias propias. Raíz de agregado.</summary>
public class Producto
{
    private Producto()
    {
        Sku = Nombre = Categoria = Descripcion = string.Empty;
    }

    public Producto(string sku, string nombre, string categoria, string descripcion, Dinero precio, decimal pesoKg, int stockInicial)
    {
        Sku = NormalizarSku(sku);
        ActualizarDatos(nombre, categoria, descripcion, precio, pesoKg);
        if (stockInicial < 0)
        {
            throw new ReglaNegocioException("STOCK_NEGATIVO", "El stock inicial debe ser un entero mayor o igual a cero.");
        }

        Stock = stockInicial;
        Activo = true;
    }

    public int Id { get; private set; }

    public string Sku { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string Categoria { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;

    public Dinero Precio { get; private set; }

    public decimal PesoKg { get; private set; }

    public int Stock { get; private set; }

    public bool Activo { get; private set; }

    /// <summary>Token de concurrencia optimista: cambia con cada modificación de stock.</summary>
    public int Version { get; private set; }

    public static string NormalizarSku(string sku)
    {
        var valor = Guardas.Texto(sku, "SKU", 30).ToUpperInvariant();
        if (valor.Any(char.IsWhiteSpace))
        {
            throw new ReglaNegocioException("SKU_INVALIDO", "El SKU no puede contener espacios.");
        }

        return valor;
    }

    public void ActualizarDatos(string nombre, string categoria, string descripcion, Dinero precio, decimal pesoKg)
    {
        if (precio.Valor <= 0)
        {
            throw new ReglaNegocioException("PRECIO_INVALIDO", "El precio debe ser mayor que cero.");
        }

        if (pesoKg < 0)
        {
            throw new ReglaNegocioException("PESO_INVALIDO", "El peso no puede ser negativo.");
        }

        Nombre = Guardas.Texto(nombre, "Nombre", 120);
        Categoria = Guardas.Texto(categoria, "Categoría", 60);
        Descripcion = descripcion?.Trim() ?? string.Empty;
        Precio = precio.Redondeado();
        PesoKg = pesoKg;
    }

    public void Desactivar() => Activo = false;

    public void Activar() => Activo = true;

    public bool TieneStock(int cantidad) => Stock >= cantidad;

    /// <summary>Descuenta existencias y devuelve el movimiento negativo trazable.</summary>
    public MovimientoInventario Descontar(int cantidad, MotivoMovimiento motivo, string referencia, string usuario, DateTime fechaUtc)
    {
        Guardas.Positivo(cantidad, "La cantidad a descontar");
        if (!TieneStock(cantidad))
        {
            throw new ReglaNegocioException(
                "STOCK_INSUFICIENTE",
                $"Stock insuficiente de {Sku}: se requieren {cantidad} y hay {Stock}.");
        }

        Stock -= cantidad;
        Version++;
        return new MovimientoInventario(this, -cantidad, motivo, referencia, usuario, fechaUtc);
    }

    /// <summary>Reintegra existencias y devuelve el movimiento positivo trazable.</summary>
    public MovimientoInventario Reintegrar(int cantidad, MotivoMovimiento motivo, string referencia, string usuario, DateTime fechaUtc)
    {
        Guardas.Positivo(cantidad, "La cantidad a reintegrar");
        Stock += cantidad;
        Version++;
        return new MovimientoInventario(this, cantidad, motivo, referencia, usuario, fechaUtc);
    }

    /// <summary>Proyección inmutable como hoja del Composite de catálogo.</summary>
    public ProductoSimple ComoElemento() => new(Id, Sku, Nombre, Categoria, Precio, PesoKg);
}
