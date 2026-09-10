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
        
        // El correo con longitud, y no `longtext`.
        //
        // Es lo que permite el índice único: MySQL no indexa un `longtext` sin decirle cuántos
        // caracteres mirar, y un índice por prefijo daría por repetidos dos correos que coinciden
        // en los primeros N. 320 es el máximo de la norma —64 de buzón, arroba, 255 de dominio—,
        // así que no recorta ningún correo válido.
        builder.ComplexProperty(u => u.Email, p =>
        {
            p.Property(e => e.Value).HasColumnName("Email").HasMaxLength(320).IsRequired();
        });

        // **El correo es único en toda la base, no por inquilino.** Podría parecer que dos
        // inquilinos deberían poder tener a la misma persona, pero el inicio de sesión busca sólo
        // por correo —no hay dónde escribir el inquilino en la pantalla de entrada—, así que dos
        // filas con el mismo correo hacen que quien entra sea una de las dos, al azar.
        //
        // No es teórico: la base de desarrollo llegó a tener 115 filas «admin@acme.com» y quien
        // iniciaba sesión no era el administrador que posee los proyectos, de modo que «Mis
        // proyectos» enseñaba 0 teniendo cinco. El sembrador ya no las crea, pero **el índice es
        // lo que impide que vuelva a pasar por cualquier otro camino**: una comprobación en
        // código no sirve cuando hay dos instancias arrancando a la vez.
        //
        // El índice (`IX_User_Email_Unico`) **se declara en la migración, en SQL**, y no aquí.
        // No es una preferencia: `Email` es un tipo complejo, y `HasIndex("Email")` intenta crear
        // una propiedad sombra con ese nombre —«a property with the same name already exists»—.
        // EF no sabe indexar la columna de un tipo complejo desde el modelo, así que el índice
        // vive donde sí manda, que es la base.

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
