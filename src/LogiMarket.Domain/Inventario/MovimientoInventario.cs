using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Inventario;

/// <summary>Movimiento positivo o negativo de existencias con motivo y referencia (p. ej. PED-021 P01 −1).</summary>
public class MovimientoInventario
{
    private MovimientoInventario()
    {
        Sku = Referencia = Usuario = string.Empty;
    }

    internal MovimientoInventario(Producto producto, int cantidad, MotivoMovimiento motivo, string referencia, string usuario, DateTime fechaUtc)
    {
        if (cantidad == 0)
        {
            throw new ReglaNegocioException("MOVIMIENTO_VACIO", "Un movimiento de inventario no puede ser cero.");
        }

        Producto = producto;
        ProductoId = producto.Id;
        Sku = producto.Sku;
        Cantidad = cantidad;
        Motivo = motivo;
        Referencia = Guardas.Texto(referencia, "Referencia", 40);
        Usuario = Guardas.Texto(usuario, "Usuario", 60);
        FechaUtc = fechaUtc;
        StockResultante = producto.Stock;
    }

    public long Id { get; private set; }

    public int ProductoId { get; private set; }

    public Producto? Producto { get; private set; }

    public string Sku { get; private set; }

    public int Cantidad { get; private set; }

    public MotivoMovimiento Motivo { get; private set; }

    public string Referencia { get; private set; }

    public string Usuario { get; private set; }

    public DateTime FechaUtc { get; private set; }

    public int StockResultante { get; private set; }

    public string Descripcion => $"{Referencia} {Sku} {(Cantidad > 0 ? "+" : "−")}{Math.Abs(Cantidad)}";
}
