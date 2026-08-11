using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities;

namespace Reporting.Infrastructure.Persistence.Configurations;

internal sealed class TicketReadModelConfiguration : IEntityTypeConfiguration<TicketReadModel>
{
    public void Configure(EntityTypeBuilder<TicketReadModel> builder)
    {
        builder.ToTable("ReadModel_Tickets");
        builder.HasKey(x => x.Id);
    }
}
