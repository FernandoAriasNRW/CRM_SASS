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
    public async Task Login_con_credenciales_invalidas_devuelve_401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "noexiste@acme.com",
            Password = "loQueSea123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_con_email_vacio_devuelve_400_y_no_401()
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
    public async Task Un_endpoint_protegido_sin_token_devuelve_401()
    {
        var response = await Client.GetAsync("/api/v1/projects");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task El_seed_de_base_de_datos_no_es_accesible_sin_autenticacion()
    {
        // Estaba abierto: un POST anónimo reinicializaba los datos. Se cubre para que
        // no vuelva a quedar expuesto sin que nadie se entere.
        var response = await Client.PostAsync("/api/v1/admin/seed-database", null);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task El_healthcheck_de_vida_responde_sin_autenticacion()
    {
        var response = await Client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
