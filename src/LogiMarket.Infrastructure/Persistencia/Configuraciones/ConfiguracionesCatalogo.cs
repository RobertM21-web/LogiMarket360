using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogiMarket.Infrastructure.Persistencia.Configuraciones;

internal sealed class ProductoConfig : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> b)
    {
        b.ToTable("Producto", t =>
        {
            t.HasCheckConstraint("CK_Producto_Precio", "CAST(Precio AS REAL) > 0");
            t.HasCheckConstraint("CK_Producto_Stock", "Stock >= 0");
            t.HasCheckConstraint("CK_Producto_Peso", "CAST(PesoKg AS REAL) >= 0");
        });
        b.HasKey(p => p.Id);
        b.Property(p => p.Sku).HasMaxLength(30).IsRequired();
        b.HasIndex(p => p.Sku).IsUnique();
        b.HasIndex(p => p.Nombre);
        b.HasIndex(p => p.Categoria);
        b.Property(p => p.Nombre).HasMaxLength(120).IsRequired();
        b.Property(p => p.Categoria).HasMaxLength(60).IsRequired();
        b.Property(p => p.Descripcion).HasMaxLength(500);
        b.Property(p => p.PesoKg).HasPrecision(10, 3);
        b.Property(p => p.Version).IsConcurrencyToken();
    }
}

internal sealed class KitConfig : IEntityTypeConfiguration<Kit>
{
    public void Configure(EntityTypeBuilder<Kit> b)
    {
        b.ToTable("Kit");
        b.HasKey(k => k.Id);
        b.Property(k => k.Codigo).HasMaxLength(30).IsRequired();
        b.HasIndex(k => k.Codigo).IsUnique();
        b.Property(k => k.Nombre).HasMaxLength(120).IsRequired();
        b.Property(k => k.Descripcion).HasMaxLength(500);
        b.HasMany(k => k.Componentes).WithOne(c => c.Kit).HasForeignKey(c => c.KitId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(k => k.Componentes).HasField("_componentes").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class KitComponenteConfig : IEntityTypeConfiguration<KitComponente>
{
    public void Configure(EntityTypeBuilder<KitComponente> b)
    {
        b.ToTable("KitComponente", t =>
        {
            t.HasCheckConstraint("CK_KitComponente_Exclusivo", "(ProductoId IS NULL) <> (KitHijoId IS NULL)");
            t.HasCheckConstraint("CK_KitComponente_Cantidad", "Cantidad > 0");
            t.HasCheckConstraint("CK_KitComponente_NoAutoReferencia", "KitHijoId IS NULL OR KitHijoId <> KitId");
        });
        b.HasKey(c => c.Id);
        b.HasOne(c => c.Producto).WithMany().HasForeignKey(c => c.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(c => c.KitHijo).WithMany().HasForeignKey(c => c.KitHijoId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ClienteConfig : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("Cliente");
        b.HasKey(c => c.Id);
        b.Property(c => c.Codigo).HasMaxLength(20).IsRequired();
        b.HasIndex(c => c.Codigo).IsUnique();
        b.Property(c => c.Identificacion).HasMaxLength(20).IsRequired();
        b.HasIndex(c => c.Identificacion).IsUnique();
        b.Property(c => c.Nombre).HasMaxLength(120).IsRequired();
        b.HasIndex(c => c.Nombre);
        b.Property(c => c.Email).HasMaxLength(120);
        b.Property(c => c.Telefono).HasMaxLength(30);
        b.Property(c => c.Categoria).HasConversion<string>().HasMaxLength(10);
        b.Property(c => c.CanalPreferido).HasConversion<string>().HasMaxLength(10);
        b.Ignore(c => c.Destino);
    }
}

internal sealed class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuario");
        b.HasKey(u => u.Id);
        b.Property(u => u.NombreUsuario).HasMaxLength(40).IsRequired();
        b.HasIndex(u => u.NombreUsuario).IsUnique();
        b.Property(u => u.NombreCompleto).HasMaxLength(120).IsRequired();
        b.Property(u => u.Rol).HasConversion<string>().HasMaxLength(10);
        b.Property(u => u.HashContrasena).HasMaxLength(200).IsRequired();
        b.Property(u => u.Sal).HasMaxLength(100).IsRequired();
    }
}

internal sealed class MovimientoInventarioConfig : IEntityTypeConfiguration<MovimientoInventario>
{
    public void Configure(EntityTypeBuilder<MovimientoInventario> b)
    {
        b.ToTable("MovimientoInventario", t => t.HasCheckConstraint("CK_Movimiento_NoCero", "Cantidad <> 0"));
        b.HasKey(m => m.Id);
        b.HasOne(m => m.Producto).WithMany().HasForeignKey(m => m.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(m => m.Sku).HasMaxLength(30);
        b.Property(m => m.Motivo).HasConversion<string>().HasMaxLength(20);
        b.Property(m => m.Referencia).HasMaxLength(40);
        b.Property(m => m.Usuario).HasMaxLength(60);
        b.HasIndex(m => m.FechaUtc);
        b.HasIndex(m => m.Referencia);
        b.Ignore(m => m.Descripcion);
    }
}
