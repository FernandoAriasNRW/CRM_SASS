using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Cambiar la propia contraseña, y sobre todo <b>que haga falta saber la anterior</b>.
///
/// El comando, su handler y su validador existían desde el principio y ningún endpoint los
/// exponía. La pantalla de perfil llamaba a <c>/profile/password</c>, que no es ninguna ruta.
///
/// Al conectarlo apareció lo importante: <b>el handler recibía la contraseña actual y no la
/// comprobaba</b>. Con la anterior equivocada devolvía 204 y la cambiaba igual. No llegó a estar
/// expuesto —por eso nadie lo sufrió— pero eso es suerte y no diseño: en cuanto la pantalla lo
/// llama, quien encuentre una sesión abierta se queda con la cuenta sin saber la contraseña.
///
/// Por eso la prueba central no es «cambia la contraseña»: es <b>«no la cambia si la anterior no
/// es la buena»</b>. La versión rota pasaba la primera con nota.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PasswordChangeFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AuthenticateAsync(string password = Password)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password = password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient client, string actual, string newPassword)
        => client.PutAsJsonAsync("/api/v1/users/me/password",
            new { CurrentPassword = actual, NewPassword = newPassword });

    /// <summary>Con la contraseña actual equivocada no se cambia nada. Es el fallo, con su nombre.</summary>
    [Fact]
    public async Task Without_the_current_password_it_does_not_change()
    {
        var client = await AuthenticateAsync();

        var response = await ChangeAsync(client, "esta-no-es", "loQueSea123");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "el handler recibía la contraseña actual y no la miraba: devolvía 204 y la cambiaba igual");

        // Y lo que importa de verdad: que la de siempre siga sirviendo. Comprobar sólo el código
        // de respuesta dejaría pasar un cambio que además hubiera ocurrido.
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK,
            "si la contraseña hubiera cambiado, la de siempre ya no entraría");
    }

    /// <summary>
    /// Con la actual correcta sí se cambia, y la nueva sirve para entrar.
    ///
    /// Se deja como estaba al terminar: el resto de la colección inicia sesión con la de siempre,
    /// y una prueba que cambia una credencial compartida y no la devuelve rompe a las demás según
    /// el orden en que se ejecuten.
    /// </summary>
    [Fact]
    public async Task With_the_current_password_it_changes_and_the_new_one_works()
    {
        const string NewPassword = "unaNuevaClave9";

        var client = await AuthenticateAsync();

        var change = await ChangeAsync(client, Password, NewPassword);
        change.StatusCode.Should().Be(HttpStatusCode.NoContent, await change.Content.ReadAsStringAsync());

        try
        {
            var withNew = await factory.CreateClient()
                .PostAsJsonAsync("/api/v1/auth/login", new { Email, Password = NewPassword });

            withNew.StatusCode.Should().Be(HttpStatusCode.OK, "la nueva contraseña tiene que servir");

            var withOld = await factory.CreateClient()
                .PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });

            withOld.StatusCode.Should().NotBe(HttpStatusCode.OK,
                "y la anterior tiene que dejar de servir, o no se ha cambiado nada");
        }
        finally
        {
            var back = await AuthenticateAsync(NewPassword);
            (await ChangeAsync(back, NewPassword, Password))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    /// <summary>
    /// La ruta del usuario actual es <c>/auth/users/me</c>.
    ///
    /// La pantalla de perfil pedía <c>/users/me</c>, que devuelve 404, y el aviso rojo —«El
    /// recurso solicitado no existe»— salía encima de un formulario en blanco al abrir el perfil.
    /// Se comprueban las dos: que la buena responde y que la otra no existe, porque el fallo era
    /// justamente confundirlas.
    /// </summary>
    [Fact]
    public async Task The_current_user_is_requested_by_its_route()
    {
        var client = await AuthenticateAsync();

        var good = await client.GetAsync("/api/v1/auth/users/me");
        good.StatusCode.Should().Be(HttpStatusCode.OK);

        (await good.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("email").GetString().Should().Be(Email);

        var theScreenRoute = await client.GetAsync("/api/v1/users/me");
        theScreenRoute.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "si algún día existe, el comentario de esta prueba deja de ser cierto y hay que revisarla");
    }
}
