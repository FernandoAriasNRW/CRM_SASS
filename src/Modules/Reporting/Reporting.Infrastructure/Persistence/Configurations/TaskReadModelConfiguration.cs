using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities;

namespace Reporting.Infrastructure.Persistence.Configurations;

internal sealed class TaskReadModelConfiguration : IEntityTypeConfiguration<TaskReadModel>
{
    public void Configure(EntityTypeBuilder<TaskReadModel> builder)
    {
        builder.ToTable("ReadModel_Tasks");
        builder.HasKey(x => x.Id);
    }
}
