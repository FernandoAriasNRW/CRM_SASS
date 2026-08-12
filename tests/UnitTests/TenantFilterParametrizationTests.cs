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
    private readonly DbContextOptions<ProjectsDbContext> _opciones;

    public TenantFilterParametrizationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Unas mismas opciones para todos los contextos: un solo modelo, como en producción.
        _opciones = new DbContextOptionsBuilder<ProjectsDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Este contexto es el que construye el modelo, y lo hace SIN tenant, igual que el
        // arranque de la aplicación cuando migra y siembra.
        using var arranque = Contexto(Guid.Empty);
        arranque.Database.EnsureCreated();
        arranque.Projects.Add(Proyecto(TenantA, "Proyecto de A"));
        arranque.Projects.Add(Proyecto(TenantB, "Proyecto de B"));
        arranque.SaveChanges();
    }

    private ProjectsDbContext Contexto(Guid tenantId) =>
        new(_opciones, new StubUserContext(tenantId));

    private static Project Proyecto(Guid tenantId, string nombre) =>
        Project.Create(
            tenantId,
            spaceId: Guid.NewGuid(),
            folderId: null,
            name: nombre,
            description: "creado por el test",
            estimatedEndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            ownerId: Guid.NewGuid());

    [Fact]
    public void Un_tenant_ve_sus_filas_aunque_el_modelo_lo_construyera_otro_contexto_sin_tenant()
    {
        using var contexto = Contexto(TenantA);

        var visibles = contexto.Projects.ToList();

        visibles.Should().HaveCount(1, "el tenant debe leerse en cada consulta, no al construir el modelo");
        visibles.Single().TenantId.Should().Be(TenantA);
    }

    [Fact]
    public void Dos_contextos_con_tenants_distintos_ven_cada_uno_lo_suyo()
    {
        // Con el tenant horneado, los dos verían lo mismo —nada— y el aislamiento parecería
        // correcto por el motivo equivocado.
        using var contextoA = Contexto(TenantA);
        using var contextoB = Contexto(TenantB);

        contextoA.Projects.Single().Name.Value.Should().Be("Proyecto de A");
        contextoB.Projects.Single().Name.Value.Should().Be("Proyecto de B");
    }

    [Fact]
    public void El_tenant_no_aparece_como_literal_en_el_SQL()
    {
        // La comprobación directa de la causa: si el filtro se traduce a un literal, el
        // modelo cacheado sirve el tenant de quien lo construyó a todos los demás.
        using var contexto = Contexto(TenantA);

        var sql = contexto.Projects.ToQueryString();

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
