using Microsoft.EntityFrameworkCore;
using Tags.Application.Abstractions;
using Tags.Domain.Entities;
using Tags.Domain.ValueObjects;
using Tags.Infrastructure.Persistence;

namespace ApiHost.Seeding;

/// <summary>
/// Las etiquetas de la demostración: las predefinidas del producto y un hito propio, para que la
/// lista enseñe también una etiqueta creada por la organización.
///
/// Las predefinidas se aprovisionan igualmente en cada arranque (<c>DatabaseInitialization</c>);
/// se llaman también aquí para que el endpoint de siembra deje la organización completa sin
/// esperar a reiniciar.
/// </summary>
public sealed class TagsSeeder(TagsDbContext tagsDb, IBuiltInTagProvisioner builtInTags) : IModuleSeeder
{
    public string Module => "Tags";
    public int Order => 110;

    private const string DemoMilestone = "🚀 Q3 Release";

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;

        using var _ = tagsDb.AsTenant(tenantId);
        try { await OrphanRows.AdoptAsync(tagsDb, "Tags", "TenantId", tenantId, cancellationToken); } catch { }

        await builtInTags.ProvisionAsync(tenantId, cancellationToken);

        if (await tagsDb.Tags.AnyAsync(t => t.TenantId == tenantId && t.Name == DemoMilestone, cancellationToken))
            return;

        tagsDb.Tags.Add(Tag.Create(tenantId, DemoMilestone, "#EC4899", TagCategory.Milestone));
        await tagsDb.SaveChangesAsync(cancellationToken);
    }
}
