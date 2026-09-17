using Microsoft.EntityFrameworkCore;
using Projects.Domain.Entities;
using Projects.Infrastructure.Persistence;

namespace ApiHost.Seeding;

public sealed class ProjectsSeeder(ProjectsDbContext projectsDb) : IModuleSeeder
{
    public string Module => "Projects";
    public int Order => 30;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var admin = context.Admin;

        using var _ = projectsDb.AsTenant(tenantId);
        try
        {
            await projectsDb.Database.ExecuteSqlAsync($"UPDATE `Spaces` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
            await projectsDb.Database.ExecuteSqlAsync($"UPDATE `Folders` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
            await projectsDb.Database.ExecuteSqlAsync($"UPDATE `Projects` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
        }
        catch { }

        var spaces = await projectsDb.Spaces.Where(s => s.TenantId == tenantId).ToListAsync(cancellationToken);
        if (spaces.Count == 0)
        {
            projectsDb.Spaces.AddRange(
                Space.Create(tenantId, "🚀 Plataforma SaaS Core", "Proyectos principales del motor CRM y microservicios", "#8B5CF6"),
                Space.Create(tenantId, "💼 Operaciones & Clientes", "Gestión de clientes enterprise, soporte y onboarding", "#3B82F6"),
                Space.Create(tenantId, "📊 Marketing & Producto", "Lanzamientos Q3, diseño UI/UX y analíticas de uso", "#10B981"));
            await projectsDb.SaveChangesAsync(cancellationToken);
            spaces = await projectsDb.Spaces.Where(s => s.TenantId == tenantId).ToListAsync(cancellationToken);
        }

        var coreSpace = spaces.FirstOrDefault(s => s.Name.Contains("Core")) ?? spaces[0];
        var operationsSpace = spaces.FirstOrDefault(s => s.Name.Contains("Operaciones")) ?? spaces[0];

        var folders = await projectsDb.Folders.Where(f => f.TenantId == tenantId).ToListAsync(cancellationToken);
        if (folders.Count == 0)
        {
            projectsDb.Folders.AddRange(
                Folder.Create(tenantId, coreSpace.Id, "Backend Microservicios"),
                Folder.Create(tenantId, coreSpace.Id, "Aplicación Web Angular 19"),
                Folder.Create(tenantId, operationsSpace.Id, "Onboarding Clientes VIP"));
            await projectsDb.SaveChangesAsync(cancellationToken);
            folders = await projectsDb.Folders.Where(f => f.TenantId == tenantId).ToListAsync(cancellationToken);
        }

        var backendFolder = folders.FirstOrDefault(f => f.Name.Contains("Backend")) ?? folders[0];
        var frontendFolder = folders.FirstOrDefault(f => f.Name.Contains("Angular")) ?? folders[0];

        var projects = await projectsDb.Projects.Where(p => p.TenantId == tenantId).ToListAsync(cancellationToken);
        if (projects.Count == 0)
        {
            var today = DateTime.UtcNow;
            projectsDb.Projects.AddRange(
                Project.Create(tenantId, coreSpace.Id, backendFolder.Id, "CRM SaaS Suite v2.0", "Migración a arquitectura limpia C# .NET 9 con MediatR y CQRS", DateOnly.FromDateTime(today.AddMonths(2)), admin.Id),
                Project.Create(tenantId, coreSpace.Id, frontendFolder.Id, "Rediseño ClickUp UI/UX", "Implementación de interfaz moderna con Tailwind CSS y componentes ShadCN", DateOnly.FromDateTime(today.AddMonths(1)), admin.Id),
                Project.Create(tenantId, coreSpace.Id, backendFolder.Id, "Sistema de Webhooks Globals", "Infraestructura de suscripción a eventos con seguridad HMAC-SHA256", DateOnly.FromDateTime(today.AddDays(21)), admin.Id),
                Project.Create(tenantId, operationsSpace.Id, null, "Portal de Clientes Enterprise", "Plataforma self-service para clientes corporativos con tableros interactivos", DateOnly.FromDateTime(today.AddMonths(3)), admin.Id),
                Project.Create(tenantId, coreSpace.Id, null, "Auditoría de Seguridad & Permisos", "Matriz de permisos granulares por usuario, equipo y rol", DateOnly.FromDateTime(today.AddMonths(1)), admin.Id));
            await projectsDb.SaveChangesAsync(cancellationToken);
            projects = await projectsDb.Projects.Where(p => p.TenantId == tenantId).ToListAsync(cancellationToken);
        }

        context.Projects = projects;
    }
}
