using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Projects.Domain.Entities;
using Projects.Infrastructure.Persistence;
using Xunit;

namespace UnitTests;

/// <summary>
/// Verifica el aislamiento entre tenants: es el riesgo más grave del sistema, porque
/// un fallo aquí no produce ningún error visible, sólo devuelve datos de otro cliente.
///
/// Se usa SQLite en memoria en lugar del proveedor InMemory porque éste último no
/// ejecuta SQL real y puede dar por buenos filtros que la base de datos no aplicaría.
/// </summary>
public sealed class TenantIsolationTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteConnection _connection;

    public TenantIsolationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // El esquema se crea una vez, sin filtro de tenant, para poder sembrar ambos.
        using var schema = CreateContext(Guid.Empty);
        schema.Database.EnsureCreated();

        using var seed = CreateContext(Guid.Empty);
        seed.Projects.Add(NewProject(TenantA, "Proyecto de A"));
        seed.Projects.Add(NewProject(TenantB, "Proyecto de B"));
        seed.SaveChanges();
    }

    private ProjectsDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ProjectsDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new ProjectsDbContext(options, new StubUserContext(tenantId));
    }

    private static Project NewProject(Guid tenantId, string name) =>
        Project.Create(
            tenantId,
            spaceId: Guid.NewGuid(),
            folderId: null,
            name: name,
            description: "creado por el test",
            estimatedEndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            ownerId: Guid.NewGuid());

    [Fact]
    public void A_tenant_only_sees_its_own_records()
    {
        using var context = CreateContext(TenantA);

        var visible = context.Projects.ToList();

        visible.Should().HaveCount(1);
        visible.Single().TenantId.Should().Be(TenantA);
    }

    [Fact]
    public void A_tenant_does_not_see_another_tenant_records()
    {
        using var context = CreateContext(TenantB);

        var fromOtherTenant = context.Projects.Where(p => p.TenantId == TenantA).ToList();

        fromOtherTenant.Should().BeEmpty();
    }

    [Fact]
    public void Finding_another_tenant_id_returns_nothing()
    {
        Guid idOfB;
        using (var contextB = CreateContext(TenantB))
        {
            idOfB = contextB.Projects.Single().Id;
        }

        using var contextA = CreateContext(TenantA);

        // Conocer el identificador no basta: el filtro se aplica igualmente.
        contextA.Projects.FirstOrDefault(p => p.Id == idOfB).Should().BeNull();
    }

    [Fact]
    public void Without_user_context_nothing_is_visible()
    {
        // Guid.Empty no casa con ninguna fila. El filtro cierra por defecto: un fallo
        // al resolver el tenant deja sin datos, no da acceso a todos.
        using var context = CreateContext(Guid.Empty);

        context.Projects.ToList().Should().BeEmpty();
    }

    [Fact]
    public void Soft_delete_still_applies_with_the_tenant_filter()
    {
        using (var context = CreateContext(TenantA))
        {
            var project = context.Projects.Single();
            project.Delete(Guid.NewGuid());
            context.SaveChanges();
        }

        using var after = CreateContext(TenantA);
        after.Projects.ToList().Should().BeEmpty(
            "los dos filtros se componen; aplicar el de tenant no debe anular el de soft delete");
    }

    [Fact]
    public void The_model_leaves_no_entity_unisolated()
    {
        using var context = CreateContext(TenantA);

        TenantIsolationVerifier.FindViolations(context).Should().BeEmpty();
    }

    public void Dispose() => _connection.Dispose();

    private sealed class StubUserContext(Guid tenantId) : IUserContext
    {
        public Guid UserId => Guid.NewGuid();
        public Guid TenantId => tenantId;
        public string Role => "Admin";
    }
}
