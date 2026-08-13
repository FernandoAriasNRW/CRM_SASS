using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WorkItems.Domain.Entities;
using WorkItems.Domain.ValueObjects;

namespace WorkItems.Infrastructure.Persistence;

public sealed class WorkItemsDbContext(DbContextOptions<WorkItemsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
  public DbSet<WorkTask> Tasks => Set<WorkTask>();
  public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkItemsDbContext).Assembly);
    
    modelBuilder.Entity<WorkTask>().ComplexProperty(t => t.Title);
    modelBuilder.Entity<WorkTask>().ComplexProperty(t => t.Status);

    // La prioridad llega después que las tareas, así que las filas que ya existen no la
    // traen. Con un valor por defecto en la base, la migración las rellena con «Normal» en
    // lugar de dejarlas con cadena vacía: una prioridad vacía no está en TaskPriority.All(),
    // así que no la pintaría ninguna vista ni la encontraría ningún filtro, y no daría
    // ningún error. Es exactamente cómo se colaron los TagIds nulos.
    //
    // La longitud no es decorativa: sin ella estas columnas serían `longtext`, y MySQL no
    // admite DEFAULT en TEXT (error 1101), así que la migración no se podría ni aplicar.
    modelBuilder.Entity<WorkTask>().ComplexProperty(t => t.Priority, p =>
    {
      p.Property(x => x.Value).HasMaxLength(20).HasDefaultValue(TaskPriority.PorDefecto.Value);
      p.Property(x => x.Name).HasMaxLength(20).HasDefaultValue(TaskPriority.PorDefecto.Name);
    });

    // Por aquí van todas las consultas de subtareas y las dos subconsultas del progreso del
    // padre, que se ejecutan por cada tarea de cada listado.
    modelBuilder.Entity<WorkTask>()
        .HasIndex(t => new { t.TenantId, t.ParentTaskId })
        .HasDatabaseName("IX_Tasks_TenantId_ParentTaskId");

    // Los responsables son una colección propiedad de la tarea: se guardan en su tabla, pero se
    // alcanzan y se filtran siempre a través de ella, así que heredan su aislamiento por tenant.
    modelBuilder.Entity<WorkTask>().OwnsMany(t => t.Assignees, a =>
    {
      a.ToTable("TaskAssignees");
      a.WithOwner().HasForeignKey("WorkTaskId");
      a.Property(x => x.UserId).HasColumnName("UserId");
      a.HasKey("WorkTaskId", "UserId");
    });

    // La unicidad la garantiza la base y no sólo el handler: dos peticiones simultáneas
    // pasarían las dos la comprobación previa y dejarían la arista duplicada.
    modelBuilder.Entity<TaskDependency>()
        .HasIndex(d => new { d.TenantId, d.TaskId, d.DependsOnTaskId })
        .IsUnique()
        .HasDatabaseName("UX_TaskDependencies_Tenant_Task_DependsOn");

    // Por aquí se consulta «quién me bloquea» y «a quién bloqueo».
    modelBuilder.Entity<TaskDependency>()
        .HasIndex(d => new { d.TenantId, d.DependsOnTaskId })
        .HasDatabaseName("IX_TaskDependencies_Tenant_DependsOn");

    // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
    ApplyTenantFilters(modelBuilder);
  }
}
