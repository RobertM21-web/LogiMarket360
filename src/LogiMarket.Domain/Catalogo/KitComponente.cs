using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo;

/// <summary>Fila de composición de un kit: referencia exactamente a un producto o a otro kit.</summary>
public class KitComponente
{
    private KitComponente()
    {
    }

    internal KitComponente(Kit kit, int? productoId, int? kitHijoId, int cantidad)
    {
        if (productoId.HasValue == kitHijoId.HasValue)
        {
            throw new ReglaNegocioException("COMPONENTE_INVALIDO", "Cada componente debe referir a un producto o a un kit, no a ambos.");
        }

        if (kitHijoId.HasValue && kitHijoId == kit.Id && kit.Id != 0)
        {
            throw new ReglaNegocioException("KIT_CICLICO", $"El kit {kit.Codigo} no puede contenerse a sí mismo.");
        }

        Kit = kit;
        ProductoId = productoId;
        KitHijoId = kitHijoId;
        Cantidad = Guardas.Positivo(cantidad, "La cantidad del componente");
    }

    public int Id { get; private set; }

    public int KitId { get; private set; }

    public Kit? Kit { get; private set; }

    public int? ProductoId { get; private set; }

    public Producto? Producto { get; private set; }

    public int? KitHijoId { get; private set; }

    public Kit? KitHijo { get; private set; }

    public int Cantidad { get; private set; }
}
