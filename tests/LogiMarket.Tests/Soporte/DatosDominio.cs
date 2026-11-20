using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Promociones;

namespace LogiMarket.Tests.Soporte;

/// <summary>Clientes de la semilla y fábricas de objetos de dominio para pruebas sin base de datos.</summary>
public static class DatosDominio
{
    public static readonly DateTime Ahora = new(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);

    public static Cliente Ana() => CatalogoOficial.ConId(new Cliente("C001", "Ana López", "0102030405", CategoriaCliente.Vip, "ana.lopez@demo.local", "0990000001", CanalNotificacion.Email), 1);

    public static Cliente Luis() => CatalogoOficial.ConId(new Cliente("C002", "Luis Ruiz", "0102030406", CategoriaCliente.Normal, "luis.ruiz@demo.local", "0990000002", CanalNotificacion.Sms), 2);

    public static Cliente Carmen() => CatalogoOficial.ConId(new Cliente("C004", "Carmen Vega", "0102030408", CategoriaCliente.Normal, "carmen.vega@demo.local", "0990000004", CanalNotificacion.Email), 4);

    public static PedidoBuilder Builder() =>
        new(new CalculadoraPedido(SelectorPoliticaDescuento.Oficial()), CatalogoFamiliasEntrega.Oficial());

    public static Pedido PedidoAna(CatalogoOficial catalogo, int id = 21) =>
        CatalogoOficial.ConId(
            Builder().ParaCliente(Ana()).Agregar(catalogo.Arbol().Obtener("KIT_TELETRABAJO"), 1)
                .ConEntrega(TipoEntrega.Estandar).Construir($"PED-{id:000}", "ventas.demo", Ahora),
            id);
}
