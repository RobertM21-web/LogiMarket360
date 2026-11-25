using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>
/// Bitácora que se confirma en su propio contexto: los registros de rechazos, denegaciones y trazas de
/// pago sobreviven aunque la transacción de negocio se revierta.
/// </summary>
public sealed class AuditoriaIndependiente : IRegistroAuditoria, IAuditoriaSeguridad
{
    private readonly IDbContextFactory<LogiMarketDbContext> _fabrica;
    private readonly IReloj _reloj;

    public AuditoriaIndependiente(IDbContextFactory<LogiMarketDbContext> fabrica, IReloj reloj)
    {
        _fabrica = fabrica;
        _reloj = reloj;
    }

    public async Task RegistrarAsync(string categoria, string evento, string detalle, string usuario, string resultado = "OK", string? correlacion = null, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        db.Auditoria.Add(new RegistroAuditoria(categoria, evento, resultado, detalle, usuario, correlacion, _reloj.AhoraUtc));
        await db.SaveChangesAsync(ct);
    }

    public Task RegistrarDenegacionAsync(string usuario, string rol, Operacion operacion, string detalle, CancellationToken ct = default) =>
        RegistrarAsync("SEGURIDAD", $"ROL {rol}: {operacion} DENEGADO", detalle, usuario, "DENEGADO", null, ct);
}

/// <summary>Reloj del sistema.</summary>
public sealed class RelojSistema : IReloj
{
    public DateTime AhoraUtc => DateTime.UtcNow;
}
