//using Microsoft.EntityFrameworkCore;

//namespace BuildingBlocks.Infrastructure.Persistence;

//public static class DbSeeder
//{
//    public static async Task SeedAsync(CrmDbContext dbContext)
//    {
//        if (await dbContext.Users.AnyAsync())
//        {
//            return;
//        }

// var tenantId = Guid.Parse("f49d9184-98f4-4983-bd07-142029983ac4");

// dbContext.Tenants.Add(new TenantEntity { Id = tenantId, Name = "Acme Corp", Slug = "acme", IsActive = true }); var
// adminId = Guid.Parse("8f1d1209-72d5-43b7-aef0-f17cf76a2d42"); var devId = Guid.Parse("e52dfa1c-9665-45af-bf8f-359ab5fc8b4b");

// dbContext.Users.AddRange( new UserEntity { Id = adminId, TenantId = tenantId, Name = "Admin", Email =
// "admin@acme.com", Password = "admin123", Role = "Admin" }, new UserEntity { Id = devId, TenantId = tenantId, Name =
// "Developer", Email = "dev@acme.com", Password = "dev123", Role = "Member" });

// var projectId = Guid.NewGuid(); dbContext.Projects.Add(new ProjectEntity { Id = projectId, TenantId = tenantId, Name
// = "CRM Core", Description = "Proyecto semilla multi-tenant", StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
// EstimatedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), Status = "In Progress", OwnerId = adminId });

// dbContext.Tasks.Add(new TaskEntity { Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = projectId, Title =
// "Diseñar dashboard", Description = "Definir KPIs principales", Status = "To Do", AssigneeId = devId, CreatedById =
// adminId, EstimatedHours = 12, DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)) });

//        await dbContext.SaveChangesAsync();
//    }
//}