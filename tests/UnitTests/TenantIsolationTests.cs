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
    public void Un_tenant_solo_ve_sus_propios_registros()
    {
        using var context = CreateContext(TenantA);

        var visibles = context.Projects.ToList();

        visibles.Should().HaveCount(1);
        visibles.Single().TenantId.Should().Be(TenantA);
    }

    [Fact]
    public void Un_tenant_no_ve_los_registros_de_otro()
    {
        using var context = CreateContext(TenantB);

        var deOtroTenant = context.Projects.Where(p => p.TenantId == TenantA).ToList();

        deOtroTenant.Should().BeEmpty();
    }

    [Fact]
    public void Buscar_por_id_ajeno_no_devuelve_nada()
    {
        Guid idDeB;
        using (var contextB = CreateContext(TenantB))
        {
            idDeB = contextB.Projects.Single().Id;
        }

        using var contextA = CreateContext(TenantA);

        // Conocer el identificador no basta: el filtro se aplica igualmente.
        contextA.Projects.FirstOrDefault(p => p.Id == idDeB).Should().BeNull();
    }

    [Fact]
    public void Sin_contexto_de_usuario_no_se_ve_nada()
    {
        // Guid.Empty no casa con ninguna fila. El filtro cierra por defecto: un fallo
        // al resolver el tenant deja sin datos, no da acceso a todos.
        using var context = CreateContext(Guid.Empty);

        context.Projects.ToList().Should().BeEmpty();
    }

    [Fact]
    public void El_soft_delete_sigue_activo_junto_al_filtro_de_tenant()
    {
        using (var context = CreateContext(TenantA))
        {
            var proyecto = context.Projects.Single();
            proyecto.Delete(Guid.NewGuid());
            context.SaveChanges();
        }

        using var despues = CreateContext(TenantA);
        despues.Projects.ToList().Should().BeEmpty(
            "los dos filtros se componen; aplicar el de tenant no debe anular el de soft delete");
    }

    [Fact]
    public void El_modelo_no_deja_ninguna_entidad_sin_aislar()
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
