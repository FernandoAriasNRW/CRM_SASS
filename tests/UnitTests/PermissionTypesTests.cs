using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;
using Xunit;

namespace UnitTests;

/// <summary>
/// El vocabulario de la tabla de permisos es uno, en singular, y lo mande quien lo mande.
/// </summary>
public sealed class PermissionTypesTests
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
    [InlineData("Tags", "Tag")]
    [InlineData("Task", "Task")]
    [InlineData("Settings", "Settings")]
    public void The_plural_is_stored_as_singular(string received, string stored)
        => PermissionTypes.Normalize(received).Should().Be(stored);

    /// <summary>
    /// Se normaliza en la entidad, no en un handler: cualquier camino que cree un permiso —la
    /// pantalla, la compartición, el sembrador— pasa por aquí.
    /// </summary>
    [Fact]
    public void Creating_a_permission_with_the_plural_stores_the_singular()
    {
        var byRole = EntityPermission.CreateForRole(Guid.NewGuid(), "Member", "Tasks", Guid.Empty, "View");
        var byPerson = EntityPermission.CreateForUser(Guid.NewGuid(), Guid.NewGuid(), "Docs", Guid.NewGuid(), "Edit");
        var byTeam = EntityPermission.CreateForTeam(Guid.NewGuid(), Guid.NewGuid(), "Projects", Guid.Empty, "Full");

        byRole.EntityType.Should().Be("Task");
        byPerson.EntityType.Should().Be("Document");
        byTeam.EntityType.Should().Be("Project");
    }
}
