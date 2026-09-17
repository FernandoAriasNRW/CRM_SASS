using Microsoft.EntityFrameworkCore;
using Tags.Domain.Entities;
using Tags.Infrastructure.Persistence;

namespace ApiHost.Seeding;

public sealed class TagsSeeder(TagsDbContext tagsDb) : IModuleSeeder
{
    public string Module => "Tags";
    public int Order => 110;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;

        using var _ = tagsDb.AsTenant(tenantId);
        try { await tagsDb.Database.ExecuteSqlAsync($"UPDATE `Tags` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

        if (await tagsDb.Tags.AnyAsync(t => t.TenantId == tenantId, cancellationToken))
            return;

        tagsDb.Tags.AddRange(
            Tag.Create(tenantId, "🔥 Crítico", "#EF4444", "Priority"),
            Tag.Create(tenantId, "⚡ Backend C#", "#8B5CF6", "Tech"),
            Tag.Create(tenantId, "🎨 Frontend Angular", "#3B82F6", "Tech"),
            Tag.Create(tenantId, "🔒 Seguridad", "#10B981", "Security"),
            Tag.Create(tenantId, "⭐ VIP Client", "#F59E0B", "Business"),
            Tag.Create(tenantId, "🚀 Q3 Release", "#EC4899", "Milestone"));
        await tagsDb.SaveChangesAsync(cancellationToken);
    }
}
