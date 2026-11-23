using LogiMarket.Application.Puertos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>Ubicaciones configurables de la base de datos y de los comprobantes TXT.</summary>
public sealed class OpcionesLogiMarket
{
    public static string CarpetaDatosPredeterminada =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogiMarket360");

    public string RutaBaseDatos { get; init; } = Path.Combine(CarpetaDatosPredeterminada, "logimarket.db");

    public string CarpetaComprobantes { get; init; } = Path.Combine(CarpetaDatosPredeterminada, "comprobantes");

    public string CadenaConexion => new SqliteConnectionStringBuilder
    {
        DataSource = RutaBaseDatos,
        DefaultTimeout = 30,
        Pooling = false,
    }.ToString();

    public static void Configurar(DbContextOptionsBuilder builder, string cadenaConexion) =>
        builder.UseSqlite(cadenaConexion, o => o.MigrationsAssembly(typeof(LogiMarketDbContext).Assembly.FullName));
}

/// <summary>Aplica migraciones sobre una BD nueva o existente y ejecuta la semilla idempotente.</summary>
public sealed class InicializadorBaseDatos : IRestablecimientoDatos
{
    private readonly LogiMarketDbContext _db;
    private readonly SemillaDatos _semilla;
    private readonly OpcionesLogiMarket _opciones;

    public InicializadorBaseDatos(LogiMarketDbContext db, SemillaDatos semilla, OpcionesLogiMarket opciones)
    {
        _db = db;
        _semilla = semilla;
        _opciones = opciones;
    }

    public async Task<int> InicializarAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_opciones.RutaBaseDatos))!);
        await _db.Database.MigrateAsync(ct);
        await _db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
        return await _semilla.AplicarAsync(ct);
    }

    /// <summary>Forma segura de restablecer los datos de prueba: elimina la BD local y la recrea con la semilla.</summary>
    public async Task<int> RestablecerAsync(CancellationToken ct = default)
    {
        await _db.Database.EnsureDeletedAsync(ct);
        return await InicializarAsync(ct);
    }
}

/// <summary>Fábrica en tiempo de diseño para <c>dotnet ef migrations</c>.</summary>
public sealed class FabricaDisenoDbContext : IDesignTimeDbContextFactory<LogiMarketDbContext>
{
    public LogiMarketDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<LogiMarketDbContext>();
        OpcionesLogiMarket.Configurar(builder, "Data Source=diseno.db");
        return new LogiMarketDbContext(builder.Options);
    }
}
