using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        
        builder.ComplexProperty(u => u.Email, p => 
        {
            p.Property(e => e.Value).HasColumnName("Email").IsRequired();
        });

        builder.ComplexProperty(u => u.PasswordHash, p =>
        {
            p.Property(e => e.Value).HasColumnName("PasswordHash").IsRequired();
            p.Property(e => e.CreatedAtUtc).HasColumnName("PasswordCreatedAtUtc");
        });

        builder.Property(u => u.Role)
               .HasConversion(
                   v => v.Value,
                   v => UserRole.FromValue<UserRole>(v)
               )
               .HasColumnName("RoleId");
    }
}
