using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Inicio de sesión con credenciales <b>válidas</b>.
///
/// Estas pruebas existen por una regresión concreta: al aplicar el filtro global de
/// tenant en la Fase 2, la búsqueda del usuario por correo pasó a filtrarse por un tenant
/// que al iniciar sesión todavía no existe. El filtro no casaba con ninguna fila, el
/// usuario nunca se encontraba y el login devolvía 401 siempre.
///
/// La suite no lo detectó porque sólo comprobaba que unas credenciales inválidas
/// devolvieran 401, y eso se cumple igual con el login intacto que completamente roto.
/// Una prueba que no distingue el éxito del fallo no está comprobando nada.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class LoginFlowTests(CrmApiFactory factory)
{
    // Credenciales del seed, documentadas en el README.
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private HttpClient Client => factory.CreateClient();

    [Fact]
    public async Task A_seeded_user_can_sign_in()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email,
            Password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "el usuario existe en el seed; un 401 aquí significa que la consulta no lo encuentra");
    }

    [Fact]
    public async Task Login_returns_a_usable_token()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString();

        token.Should().NotBeNullOrWhiteSpace();

        // No basta con que el token exista: tiene que abrir un endpoint protegido. Es lo
        // que confirma que el tenant viaja en los claims y que el filtro global lo
        // reconoce en las consultas siguientes.
        var autenticado = factory.CreateClient();
        autenticado.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var protectedResponse = await autenticado.GetAsync("/api/v1/auth/users/me");

        protectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_wrong_password_is_still_rejected()
    {
        // Contrapeso de las anteriores: comprobar que el login funciona no sirve de nada
        // si de paso se dejó de verificar la contraseña.
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email,
            Password = "estaNoEsLaContraseña"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
