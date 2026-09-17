using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;
using Xunit;

namespace UnitTests;

/// <summary>
/// El vocabulario de la tabla de permisos es uno, en singular, y lo mande quien lo mande.
/// </summary>
public sealed class TiposDePermisoTests
{
    [Theory]
    [InlineData("Tasks", "Task")]
    [InlineData("Projects", "Project")]
    [InlineData("Tickets", "Ticket")]
    [InlineData("Docs", "Document")]
    [InlineData("Documents", "Document")]
    [InlineData("Webhooks", "Webhook")]
    [InlineData("Teams", "Team")]
    [InlineData("Reports", "Report")]
    [InlineData("Task", "Task")]
    [InlineData("Settings", "Settings")]
    public void El_plural_se_guarda_en_singular(string recibido, string guardado)
        => PermissionTypes.Normalize(recibido).Should().Be(guardado);

    /// <summary>
    /// Se normaliza en la entidad, no en un handler: cualquier camino que cree un permiso —la
    /// pantalla, la compartición, el sembrador— pasa por aquí.
    /// </summary>
    [Fact]
    public void Crear_un_permiso_con_el_plural_lo_guarda_en_singular()
    {
        var porRol = EntityPermission.CreateForRole(Guid.NewGuid(), "Member", "Tasks", Guid.Empty, "View");
        var porPersona = EntityPermission.CreateForUser(Guid.NewGuid(), Guid.NewGuid(), "Docs", Guid.NewGuid(), "Edit");
        var porEquipo = EntityPermission.CreateForTeam(Guid.NewGuid(), Guid.NewGuid(), "Projects", Guid.Empty, "Full");

        porRol.EntityType.Should().Be("Task");
        porPersona.EntityType.Should().Be("Document");
        porEquipo.EntityType.Should().Be("Project");
    }
}
