using Microsoft.EntityFrameworkCore;
using Teams.Domain.Entities;
using Teams.Domain.ValueObjects;
using Teams.Infrastructure.Persistence;

namespace ApiHost.Seeding;

public sealed class TeamsSeeder(TimeProvider timeProvider, TeamsDbContext teamsDb) : IModuleSeeder
{
    public string Module => "Teams";
    public int Order => 20;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;

        // El sembrador no tiene petición, así que el filtro global compara el inquilino
        // contra Guid.Empty y **todas sus consultas devuelven cero filas**. Eso rompía la
        // siembra en una base nueva: se insertaban los tres espacios, la relectura salía
        // vacía y `existingSpaces[0]` lanzaba; con Projects caído, las tareas —que dependen
        // de que haya proyectos— tampoco se creaban. Declarar el inquilino lo arregla sin
        // apagar el resto de filtros, que es lo que haría IgnoreQueryFilters.
        using var _ = teamsDb.AsTenant(tenantId);
        try { await OrphanRows.AdoptAsync(teamsDb, "Teams", "TenantId", tenantId, cancellationToken); } catch { }

        if (await teamsDb.Teams.AnyAsync(t => t.TenantId == tenantId, cancellationToken))
            return;

        var admin = context.Admin;
        var members = context.Members;

        var core = Team.Create(timeProvider.GetUtcNow().UtcDateTime, tenantId, "🚀 Core Engineering", "Desarrollo de microservicios backend C# .NET 9 y frontend Angular 19");
        var design = Team.Create(timeProvider.GetUtcNow().UtcDateTime, tenantId, "🎨 Product & UI/UX Design", "Diseño de interfaces, componentes ShadCN y experiencia de usuario");
        var sales = Team.Create(timeProvider.GetUtcNow().UtcDateTime, tenantId, "📊 Sales & Customer Success", "Atención a clientes VIP, soporte técnico y crecimiento comercial");
        var devOps = Team.Create(timeProvider.GetUtcNow().UtcDateTime, tenantId, "🔒 DevOps & Cloud Infra", "Infraestructura Cloud, despliegues Docker y seguridad de datos");

        core.AddMember(timeProvider.GetUtcNow().UtcDateTime, admin.Id, TeamRole.Owner);
        if (members.Count > 0) core.AddMember(timeProvider.GetUtcNow().UtcDateTime, members[0].Id, TeamRole.Member);
        if (members.Count > 1) core.AddMember(timeProvider.GetUtcNow().UtcDateTime, members[1].Id, TeamRole.Member);

        design.AddMember(timeProvider.GetUtcNow().UtcDateTime, admin.Id, TeamRole.Member);
        if (members.Count > 2) design.AddMember(timeProvider.GetUtcNow().UtcDateTime, members[2].Id, TeamRole.Owner);
        if (members.Count > 3) design.AddMember(timeProvider.GetUtcNow().UtcDateTime, members[3].Id, TeamRole.Member);

        sales.AddMember(timeProvider.GetUtcNow().UtcDateTime, admin.Id, TeamRole.Member);
        if (members.Count > 4) sales.AddMember(timeProvider.GetUtcNow().UtcDateTime, members[4].Id, TeamRole.Owner);

        devOps.AddMember(timeProvider.GetUtcNow().UtcDateTime, admin.Id, TeamRole.Owner);
        if (members.Count > 5) devOps.AddMember(timeProvider.GetUtcNow().UtcDateTime, members[5].Id, TeamRole.Member);

        teamsDb.Teams.AddRange(core, design, sales, devOps);
        await teamsDb.SaveChangesAsync(cancellationToken);
    }
}
