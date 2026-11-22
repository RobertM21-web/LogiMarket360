using LogiMarket.Application.Registros;
using LogiMarket.Domain.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogiMarket.Infrastructure.Persistencia.Configuraciones;

internal sealed class PedidoConfig : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> b)
    {
        b.ToTable("Pedido");
        b.HasKey(p => p.Id);
        b.Property(p => p.Codigo).HasMaxLength(20).IsRequired();
        b.HasIndex(p => p.Codigo).IsUnique();
        b.HasIndex(p => p.CodigoEstado);
        b.HasIndex(p => p.FechaCreacionUtc);
        b.Property(p => p.ClaveIdempotencia).HasMaxLength(40).IsRequired();
        b.HasIndex(p => p.ClaveIdempotencia).IsUnique();
        b.Property(p => p.NombreCliente).HasMaxLength(120);
        b.Property(p => p.CategoriaCliente).HasConversion<string>().HasMaxLength(10);
        b.Property(p => p.TipoEntrega).HasConversion<string>().HasMaxLength(10);
        b.Property(p => p.CodigoEstado).HasConversion<string>().HasMaxLength(12).HasColumnName("Estado");
        b.Property(p => p.PoliticaDescuento).HasMaxLength(20);
        b.Property(p => p.TasaDescuento).HasPrecision(5, 4);
        b.Property(p => p.CreadoPor).HasMaxLength(40);

        b.HasOne(p => p.Cliente).WithMany().HasForeignKey(p => p.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(p => p.Detalles).WithOne().HasForeignKey(d => d.PedidoId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(p => p.Detalles).HasField("_detalles").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(p => p.Historial).WithOne().HasForeignKey(h => h.PedidoId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(p => p.Historial).HasField("_historial").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasOne(p => p.Pago).WithOne(p => p.Pedido).HasForeignKey<Pago>(p => p.PedidoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Despacho).WithOne(d => d.Pedido).HasForeignKey<Despacho>(d => d.PedidoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Devolucion).WithOne(d => d.Pedido).HasForeignKey<Devolucion>(d => d.PedidoId).OnDelete(DeleteBehavior.Restrict);

        b.Ignore(p => p.Estado);
        b.Ignore(p => p.Eventos);
        b.Ignore(p => p.FueDevuelto);
        b.Ignore(p => p.ImporteProductos);
    }
}

internal sealed class PedidoDetalleConfig : IEntityTypeConfiguration<PedidoDetalle>
{
    public void Configure(EntityTypeBuilder<PedidoDetalle> b)
    {
        b.ToTable("PedidoDetalle", t => t.HasCheckConstraint("CK_PedidoDetalle_Cantidad", "Cantidad > 0"));
        b.HasKey(d => d.Id);
        b.Property(d => d.Codigo).HasMaxLength(30).IsRequired();
        b.Property(d => d.Nombre).HasMaxLength(120);
        b.HasMany(d => d.Componentes).WithOne().HasForeignKey(c => c.PedidoDetalleId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(d => d.Componentes).HasField("_componentes").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PedidoDetalleComponenteConfig : IEntityTypeConfiguration<PedidoDetalleComponente>
{
    public void Configure(EntityTypeBuilder<PedidoDetalleComponente> b)
    {
        b.ToTable("PedidoDetalleComponente", t => t.HasCheckConstraint("CK_PDC_Cantidad", "CantidadPorUnidad > 0"));
        b.HasKey(c => c.Id);
        b.Property(c => c.Sku).HasMaxLength(30).IsRequired();
        b.Property(c => c.Nombre).HasMaxLength(120);
        b.HasOne<Domain.Catalogo.Producto>().WithMany().HasForeignKey(c => c.ProductoId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HistorialEstadoConfig : IEntityTypeConfiguration<HistorialEstadoPedido>
{
    public void Configure(EntityTypeBuilder<HistorialEstadoPedido> b)
    {
        b.ToTable("HistorialEstadoPedido");
        b.HasKey(h => h.Id);
        b.Property(h => h.EstadoAnterior).HasConversion<string>().HasMaxLength(12);
        b.Property(h => h.EstadoNuevo).HasConversion<string>().HasMaxLength(12);
        b.Property(h => h.Usuario).HasMaxLength(40);
    }
}

internal sealed class PagoConfig : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> b)
    {
        b.ToTable("Pago");
        b.HasKey(p => p.Id);
        b.HasIndex(p => p.PedidoId).IsUnique();
        b.Property(p => p.Referencia).HasMaxLength(60).IsRequired();
        b.HasIndex(p => p.Referencia).IsUnique();
        b.Property(p => p.ClaveIdempotencia).HasMaxLength(40).IsRequired();
        b.HasIndex(p => p.ClaveIdempotencia).IsUnique();
        b.Property(p => p.Proveedor).HasMaxLength(40);
        b.Property(p => p.Usuario).HasMaxLength(40);
    }
}

internal sealed class DespachoConfig : IEntityTypeConfiguration<Despacho>
{
    public void Configure(EntityTypeBuilder<Despacho> b)
    {
        b.ToTable("Despacho");
        b.HasKey(d => d.Id);
        b.HasIndex(d => d.PedidoId).IsUnique();
        b.Property(d => d.NumeroGuia).HasMaxLength(30).IsRequired();
        b.HasIndex(d => d.NumeroGuia).IsUnique();
        b.Property(d => d.TipoEntrega).HasConversion<string>().HasMaxLength(10);
        b.Property(d => d.Etiqueta).HasMaxLength(300);
        b.Property(d => d.UsuarioDespacho).HasMaxLength(40);
        b.Property(d => d.UsuarioEntrega).HasMaxLength(40);
    }
}

internal sealed class DevolucionConfig : IEntityTypeConfiguration<Devolucion>
{
    public void Configure(EntityTypeBuilder<Devolucion> b)
    {
        b.ToTable("Devolucion");
        b.HasKey(d => d.Id);
        b.Property(d => d.Codigo).HasMaxLength(20).IsRequired();
        b.HasIndex(d => d.Codigo).IsUnique();
        b.HasIndex(d => d.PedidoId).IsUnique();
        b.Property(d => d.ReferenciaReembolso).HasMaxLength(60).IsRequired();
        b.HasIndex(d => d.ReferenciaReembolso).IsUnique();
        b.Property(d => d.Motivo).HasConversion<string>().HasMaxLength(20);
        b.Property(d => d.Observacion).HasMaxLength(500);
        b.Property(d => d.Usuario).HasMaxLength(40);
    }
}

internal sealed class EventoOutboxConfig : IEntityTypeConfiguration<EventoOutbox>
{
    public void Configure(EntityTypeBuilder<EventoOutbox> b)
    {
        b.ToTable("EventoOutbox");
        b.HasKey(e => e.Id);
        b.Property(e => e.Tipo).HasMaxLength(60).IsRequired();
        b.Property(e => e.Referencia).HasMaxLength(40);
        b.Property(e => e.Estado).HasConversion<string>().HasMaxLength(12);
        b.Property(e => e.UltimoError).HasMaxLength(500);
        b.Property(e => e.ObservadoresCompletados).HasMaxLength(200);
        b.HasIndex(e => e.Estado);
    }
}

internal sealed class AuditoriaConfig : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> b)
    {
        b.ToTable("Auditoria");
        b.HasKey(a => a.Id);
        b.Property(a => a.Categoria).HasMaxLength(20);
        b.Property(a => a.Evento).HasMaxLength(80);
        b.Property(a => a.Resultado).HasMaxLength(30);
        b.Property(a => a.Detalle).HasMaxLength(1000);
        b.Property(a => a.Usuario).HasMaxLength(40);
        b.Property(a => a.Correlacion).HasMaxLength(60);
        b.HasIndex(a => a.FechaUtc);
        b.HasIndex(a => a.Categoria);
    }
}

internal sealed class NotificacionConfig : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        b.ToTable("Notificacion");
        b.HasKey(n => n.Id);
        b.Property(n => n.Canal).HasMaxLength(10);
        b.Property(n => n.Destinatario).HasMaxLength(120);
        b.Property(n => n.Asunto).HasMaxLength(120);
        b.Property(n => n.Mensaje).HasMaxLength(500);
        b.Property(n => n.Referencia).HasMaxLength(40);
    }
}
