using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Flujos de autenticación contra la API real. Cubren lo que las pruebas unitarias no
/// pueden ver: que el middleware, las políticas y el pipeline estén bien cableados.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthEndpointsTests(CrmApiFactory factory)
{
    private HttpClient Client => factory.CreateClient();

    [Fact]
    public async Task Login_with_invalid_credentials_returns_401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "noexiste@acme.com",
            Password = "loQueSea123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_empty_email_returns_400_not_401()
    {
        // Distinguir 400 de 401 confirma que ValidationBehavior está en el pipeline:
        // antes de la Fase 1 los validadores existían pero no se ejecutaban, y una
        // entrada inválida llegaba al handler.
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "",
            Password = ""
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_protected_endpoint_without_token_returns_401()
    {
        var response = await Client.GetAsync("/api/v1/projects");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_database_seed_is_not_reachable_without_authentication()
    {
        // Estaba abierto: un POST anónimo reinicializaba los datos. Se cubre para que
        // no vuelva a quedar expuesto sin que nadie se entere.
        var response = await Client.PostAsync("/api/v1/admin/seed-database", null);

        // Las pruebas lo encienden (`DemoData:AllowSeedEndpoint`), así que existe: tiene que
        // pedir credenciales, no responder 404.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_liveness_healthcheck_answers_without_authentication()
    {
        var response = await Client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
