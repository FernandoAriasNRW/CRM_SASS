using Calendar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Calendar.Infrastructure.Persistence.Configuration;

/// <summary>
/// Configuración de Entity Framework para CalendarEvent.
/// </summary>
public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        // Tabla
        builder.ToTable("calendar_events");

        // Clave primaria
        builder.HasKey(e => e.Id);

        // Índices
        builder.HasIndex(e => e.TenantId).HasDatabaseName("ix_calendar_events_tenant_id");
        builder.HasIndex(e => new { e.TenantId, e.StartTime }).HasDatabaseName("ix_calendar_events_tenant_start");
        builder.HasIndex(e => new { e.TenantId, e.TypeValue }).HasDatabaseName("ix_calendar_events_tenant_type");
        builder.HasIndex(e => e.IsDeleted).HasDatabaseName("ix_calendar_events_is_deleted");

        // Propiedades
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.OrganizerId).HasColumnName("organizer_id").IsRequired();
        builder.Property(e => e.ProjectId).HasColumnName("project_id");
        builder.Property(e => e.TaskId).HasColumnName("task_id");
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(e => e.TypeValue).HasColumnName("type_value").IsRequired();
        builder.Property(e => e.StartTime).HasColumnName("start_time").IsRequired();
        builder.Property(e => e.EndTime).HasColumnName("end_time").IsRequired();
        builder.Property(e => e.Location).HasColumnName("location").HasMaxLength(500);
        builder.Property(e => e.IsAllDay).HasColumnName("is_all_day").HasDefaultValue(false);
        builder.Property(e => e.RecurrenceValue).HasColumnName("recurrence_value").HasDefaultValue(1);
        builder.Property(e => e.RecurrenceInterval).HasColumnName("recurrence_interval");
        builder.Property(e => e.RecurrenceEndDate).HasColumnName("recurrence_end_date");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        // Soft Delete
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");
        builder.Property(e => e.DeletedBy).HasColumnName("deleted_by");

        // Ignore de propiedades calculadas (Value Objects)
        builder.Ignore(e => e.Type);
        builder.Ignore(e => e.Recurrence);
    }
}