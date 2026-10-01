using BuildingBlocks.Application.Abstractions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Projects.Domain.Entities;
using Projects.Infrastructure.Persistence;
using Xunit;

namespace UnitTests;

/// <summary>
/// El filtro de tenant tiene que leerse en cada consulta, no al construir el modelo.
///
/// EF Core construye el modelo una sola vez por tipo de contexto y lo cachea para todo el
/// proceso. Si el tenant se resuelve mientras se construye, queda **horneado como constante**
/// en el SQL de todas las consultas siguientes, con el tenant que hubiera en ese momento.
///
/// En la aplicación real ese momento es el arranque —migraciones y siembra—, donde no hay
/// petición ni usuario y el tenant es <c>Guid.Empty</c>: si se horneara ahí, ninguna consulta
/// devolvería una fila durante el resto de la vida del proceso.
///
/// Estas pruebas nacieron al investigar justo ese síntoma —todas las listas vacías— el
/// 2026-08-12. La causa resultó ser otra (el claim del tenant se buscaba con una mayúscula que
/// el token no usa; ver <c>UserContext</c>), y la medición confirmó que EF **sí** parametriza
/// el filtro. Se quedan porque el riesgo es real, silencioso y no estaba cubierto:
/// <see cref="TenantIsolationTests"/> crea unas opciones nuevas por contexto y cada una acaba
/// con su propio modelo, así que el tenant horneado coincidiría con el que consulta y la
/// prueba pasaría igual. Aquí se comparten las opciones a propósito, como en producción.
/// </summary>
public sealed class TenantFilterParametrizationTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ProjectsDbContext> _options;

    public TenantFilterParametrizationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Unas mismas opciones para todos los contextos: un solo modelo, como en producción.
        _options = new DbContextOptionsBuilder<ProjectsDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Este contexto es el que construye el modelo, y lo hace SIN tenant, igual que el
        // arranque de la aplicación cuando migra y siembra.
        using var bootstrap = SampleContext(Guid.Empty);
        bootstrap.Database.EnsureCreated();
        bootstrap.Projects.Add(SampleProject(TenantA, "Proyecto de A"));
        bootstrap.Projects.Add(SampleProject(TenantB, "Proyecto de B"));
        bootstrap.SaveChanges();
    }

    private ProjectsDbContext SampleContext(Guid tenantId) =>
        new(_options, new StubUserContext(tenantId));

    private static Project SampleProject(Guid tenantId, string name) =>
        Project.Create(
            tenantId,
            spaceId: Guid.NewGuid(),
            folderId: null,
            name: name,
            description: "creado por el test",
            estimatedEndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            ownerId: Guid.NewGuid());

    [Fact]
    public void A_tenant_sees_its_rows_even_if_the_model_was_built_without_tenant()
    {
        using var context = SampleContext(TenantA);

        var visible = context.Projects.ToList();

        visible.Should().HaveCount(1, "el tenant debe leerse en cada consulta, no al construir el modelo");
        visible.Single().TenantId.Should().Be(TenantA);
    }

    [Fact]
    public void Two_contexts_with_different_tenants_each_see_their_own()
    {
        // Con el tenant horneado, los dos verían lo mismo —nada— y el aislamiento parecería
        // correcto por el motivo equivocado.
        using var contextA = SampleContext(TenantA);
        using var contextB = SampleContext(TenantB);

        contextA.Projects.Single().Name.Value.Should().Be("Proyecto de A");
        contextB.Projects.Single().Name.Value.Should().Be("Proyecto de B");
    }

    [Fact]
    public void The_tenant_does_not_appear_as_a_literal_in_the_SQL()
    {
        // La comprobación directa de la causa: si el filtro se traduce a un literal, el
        // modelo cacheado sirve el tenant de quien lo construyó a todos los demás.
        using var context = SampleContext(TenantA);

        var sql = context.Projects.ToQueryString();

        sql.Should().NotContain("00000000-0000-0000-0000-000000000000",
            "el filtro de tenant se horneó como constante en lugar de parametrizarse");
    }

    public void Dispose() => _connection.Dispose();

    private sealed class StubUserContext(Guid tenantId) : IUserContext
    {
        public Guid UserId => Guid.NewGuid();
        public Guid TenantId => tenantId;
        public string Role => "Admin";
    }
}
