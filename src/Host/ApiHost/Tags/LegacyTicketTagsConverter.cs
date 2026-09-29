using Microsoft.EntityFrameworkCore;
using Tags.Infrastructure.Persistence;
using Ticketing.Infrastructure.Persistence;

namespace ApiHost.Tags;

/// <summary>
/// Pasa las etiquetas antiguas de los tickets —claves fijas de la pantalla guardadas como texto,
/// «billing,urgent»— a etiquetas de verdad del módulo Tags (<c>TagIds</c>), y vacía el texto.
///
/// <b>Vive en el host y corre al arrancar, no en una migración</b>, por dos motivos: cruza dos
/// módulos (Ticketing y Tags), y las etiquetas de destino son predefinidas que se aprovisionan al
/// arrancar, después de las migraciones; en el momento de migrar todavía no existen. Es
/// idempotente: sólo mira los tickets con texto, y los deja vacíos.
///
/// <b>«urgent» se descarta a propósito:</b> la prioridad es un campo del ticket, y no hay categoría
/// de prioridad en las etiquetas. Lo que no se puede traducir se registra, no se pierde en silencio.
/// </summary>
public sealed class LegacyTicketTagsConverter(
    TicketingDbContext ticketing, TagsDbContext tags, ILogger<LegacyTicketTagsConverter> logger)
{
    /// <summary>Clave antigua de la pantalla → clave de la predefinida que la sustituye.</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyMap = new Dictionary<string, string>
    {
        ["billing"] = "billing",
        ["technical"] = "technical",
        ["account"] = "account",
        ["onboarding"] = "onboarding",
        ["integration"] = "integration",
        ["data-loss"] = "data-loss",
        ["performance"] = "performance",
        ["ui"] = "ui",
        ["waiting-client"] = "waiting-client",
        ["escalated"] = "escalated",
        ["bug"] = "bug",
        ["feature-request"] = "feature",
        ["security"] = "security-incident",
    };

    /// <returns>Cuántos tickets se han convertido.</returns>
    public async Task<int> ConvertAsync(CancellationToken ct = default)
    {
        // Sin filtros: corre sin petición, y también cuentan los tickets archivados y en la papelera.
        var pending = await ticketing.Tickets.IgnoreQueryFilters()
            .Where(t => t.Tags != "")
            .ToListAsync(ct);

        if (pending.Count == 0)
            return 0;

        foreach (var tenant in pending.GroupBy(t => t.TenantId))
        {
            var builtIns = await tags.Tags.IgnoreQueryFilters()
                .Where(t => t.TenantId == tenant.Key && t.BuiltInKey != null)
                .ToDictionaryAsync(t => t.BuiltInKey!, t => t.Id, ct);

            foreach (var ticket in tenant)
            {
                var ids = new List<Guid>();
                var dropped = new List<string>();

                foreach (var key in ticket.TagList)
                {
                    if (KeyMap.TryGetValue(key, out var builtInKey) && builtIns.TryGetValue(builtInKey, out var tagId))
                        ids.Add(tagId);
                    else
                        dropped.Add(key);
                }

                if (dropped.Count > 0)
                    logger.LogWarning(
                        "Ticket {TicketId}: etiquetas antiguas sin equivalente, descartadas: {Keys}",
                        ticket.Id, string.Join(", ", dropped));

                ticket.ReplaceLegacyTags(ids);
            }
        }

        await ticketing.SaveChangesAsync(ct);
        return pending.Count;
    }
}
