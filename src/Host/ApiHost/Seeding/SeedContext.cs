using Identity.Domain.Entities;
using Projects.Domain.Entities;

namespace ApiHost.Seeding;

/// <summary>
/// Lo que un sembrador deja para los siguientes.
///
/// Identity fija el inquilino y los usuarios, de los que cuelga todo lo demás; Projects deja los
/// proyectos, a los que se enganchan las tareas. Nada más cruza de un módulo a otro.
/// </summary>
public sealed class SeedContext
{
    public Guid TenantId { get; set; }

    /// <summary>El propietario de los proyectos y quien define el inquilino.</summary>
    public User Admin { get; set; } = null!;

    public IReadOnlyList<User> AllUsers { get; set; } = [];

    /// <summary>Todos menos el administrador.</summary>
    public IReadOnlyList<User> Members { get; set; } = [];

    public IReadOnlyList<Project> Projects { get; set; } = [];
}
