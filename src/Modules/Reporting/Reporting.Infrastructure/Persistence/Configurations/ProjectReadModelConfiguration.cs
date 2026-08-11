using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities;

namespace Reporting.Infrastructure.Persistence.Configurations;

internal sealed class ProjectReadModelConfiguration : IEntityTypeConfiguration<ProjectReadModel>
{
    public void Configure(EntityTypeBuilder<ProjectReadModel> builder)
    {
        builder.ToTable("ReadModel_Projects");
        builder.HasKey(x => x.Id);
    }
}
