using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MySql;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Levanta la API completa contra un MySQL real en contenedor.
///
/// Se usa MySQL y no SQLite ni el proveedor InMemory a propósito: el objetivo de estas
/// pruebas es detectar lo que sólo falla contra el motor de verdad —tipos de columna,
/// colaciones, traducción de consultas— y un sustituto en memoria daría por buenas
/// consultas que la base rechaza.
/// </summary>
public sealed class CrmApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("crm_test")
        .WithUsername("crm")
        .WithPassword("crm-test-password")
        .Build();

    public string ConnectionString => _mysql.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("Jwt:Key", "clave-solo-para-pruebas-de-integracion-32+caracteres");
        builder.UseSetting("Jwt:Issuer", "CrmApi");
        builder.UseSetting("Jwt:Audience", "CrmClients");

        // El límite de peticiones se sube porque todas las pruebas se autentican como el mismo
        // administrador y comparten partición: la suite entera cae dentro de la misma ventana de
        // un minuto. Con el límite de producción empezaron a salir 429 según el orden de
        // ejecución, un fallo que no dice nada del código y que volvería cada pocas pruebas
        // nuevas. No se desactiva del todo para que el middleware siga en el camino.
        builder.UseSetting("RateLimiting:PermitLimit", "100000");
    }

    public async Task InitializeAsync() => await _mysql.StartAsync();

    public new async Task DisposeAsync()
    {
        await _mysql.DisposeAsync();
        await base.DisposeAsync();
    }
}

/// <summary>
/// Comparte un único contenedor entre todas las pruebas de la colección: arrancar MySQL
/// por cada clase multiplicaría el tiempo de la suite sin aportar aislamiento real.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<CrmApiFactory>
{
    public const string Name = "api";
}
