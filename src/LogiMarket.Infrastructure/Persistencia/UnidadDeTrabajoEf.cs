using LogiMarket.Application.Puertos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>Unidad de trabajo sobre el <see cref="LogiMarketDbContext"/> y su transacción SQLite.</summary>
public sealed class UnidadDeTrabajoEf : IUnidadDeTrabajo
{
    private const int SqliteConstraint = 19;
    private readonly LogiMarketDbContext _db;

    public UnidadDeTrabajoEf(LogiMarketDbContext db)
    {
        _db = db;
    }

    public async Task<T> EnTransaccionAsync<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            return await operacion(ct);
        }

        await using var transaccion = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var resultado = await operacion(ct);
            await transaccion.CommitAsync(ct);
            return resultado;
        }
        catch
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task GuardarCambiosAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoPersistenciaException("CONCURRENCIA", "Otro usuario modificó los mismos datos (stock). Intente nuevamente.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraint } sql)
        {
            throw new ConflictoPersistenciaException("UNICIDAD", $"Restricción de integridad: {sql.Message}", ex);
        }
    }

    public void DescartarCambios()
    {
        foreach (var entrada in _db.ChangeTracker.Entries().ToList())
        {
            switch (entrada.State)
            {
                case EntityState.Added:
                    entrada.State = EntityState.Detached;
                    break;
                case EntityState.Modified:
                case EntityState.Deleted:
                    entrada.CurrentValues.SetValues(entrada.OriginalValues);
                    entrada.State = EntityState.Unchanged;
                    break;
            }
        }
    }
}
