using System.Reflection;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Tests.Soporte;

/// <summary>Catálogo inicial oficial de la guía (§3.1) construido en memoria para pruebas de dominio.</summary>
public sealed class CatalogoOficial
{
    public CatalogoOficial()
    {
        P01 = ConId(new Producto("P01", "Mouse USB", "Periféricos", "Mouse óptico USB", Dinero.Usd(20.00m), 0.20m, 8), 1);
        P02 = ConId(new Producto("P02", "Teclado", "Periféricos", "Teclado USB", Dinero.Usd(30.00m), 0.70m, 5), 2);
        P03 = ConId(new Producto("P03", "Memoria USB", "Almacenamiento", "Memoria USB 32 GB", Dinero.Usd(10.00m), 0.10m, 20), 3);
        P04 = ConId(new Producto("P04", "Webcam", "Video", "Webcam HD", Dinero.Usd(40.00m), 0.35m, 4), 4);
        P05 = ConId(new Producto("P05", "Alfombrilla", "Accesorios", "Alfombrilla de tela", Dinero.Usd(5.00m), 0.10m, 30), 5);

        KitOficina = ConId(new Kit("KIT_OFICINA", "Kit oficina", "Mouse + teclado"), 1);
        KitTeletrabajo = ConId(new Kit("KIT_TELETRABAJO", "Kit teletrabajo", "Kit oficina + memoria USB"), 2);
        ArbolCatalogo.RedefinirKit(KitOficina, [ComposicionPropuesta.DeProducto(1, 1), ComposicionPropuesta.DeProducto(2, 1)], Productos, Kits);
        ArbolCatalogo.RedefinirKit(KitTeletrabajo, [ComposicionPropuesta.DeKit(1, 1), ComposicionPropuesta.DeProducto(3, 1)], Productos, Kits);
    }

    public Producto P01 { get; }

    public Producto P02 { get; }

    public Producto P03 { get; }

    public Producto P04 { get; }

    public Producto P05 { get; }

    public Kit KitOficina { get; }

    public Kit KitTeletrabajo { get; }

    public IReadOnlyList<Producto> Productos => [P01, P02, P03, P04, P05];

    public IReadOnlyList<Kit> Kits => [KitOficina, KitTeletrabajo];

    public ArbolCatalogo Arbol() => ArbolCatalogo.Construir(Productos, Kits);

    public static T ConId<T>(T entidad, int id)
    {
        typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!.SetValue(entidad, id);
        return entidad;
    }
}
