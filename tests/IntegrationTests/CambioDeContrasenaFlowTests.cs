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
public sealed class CambioDeContrasenaFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AutenticarAsync(string contrasena = Password)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password = contrasena });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static Task<HttpResponseMessage> CambiarAsync(HttpClient cliente, string actual, string nueva)
        => cliente.PutAsJsonAsync("/api/v1/users/me/password",
            new { CurrentPassword = actual, NewPassword = nueva });

    /// <summary>Con la contraseña actual equivocada no se cambia nada. Es el fallo, con su nombre.</summary>
    [Fact]
    public async Task Sin_la_contrasena_actual_no_se_cambia()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await CambiarAsync(cliente, "esta-no-es", "loQueSea123");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest,
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
    public async Task Con_la_contrasena_actual_se_cambia_y_la_nueva_sirve()
    {
        const string Nueva = "unaNuevaClave9";

        var cliente = await AutenticarAsync();

        var cambio = await CambiarAsync(cliente, Password, Nueva);
        cambio.StatusCode.Should().Be(HttpStatusCode.NoContent, await cambio.Content.ReadAsStringAsync());

        try
        {
            var conLaNueva = await factory.CreateClient()
                .PostAsJsonAsync("/api/v1/auth/login", new { Email, Password = Nueva });

            conLaNueva.StatusCode.Should().Be(HttpStatusCode.OK, "la nueva contraseña tiene que servir");

            var conLaVieja = await factory.CreateClient()
                .PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });

            conLaVieja.StatusCode.Should().NotBe(HttpStatusCode.OK,
                "y la anterior tiene que dejar de servir, o no se ha cambiado nada");
        }
        finally
        {
            var deVuelta = await AutenticarAsync(Nueva);
            (await CambiarAsync(deVuelta, Nueva, Password))
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
    public async Task El_usuario_actual_se_pide_por_su_ruta()
    {
        var cliente = await AutenticarAsync();

        var buena = await cliente.GetAsync("/api/v1/auth/users/me");
        buena.StatusCode.Should().Be(HttpStatusCode.OK);

        (await buena.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("email").GetString().Should().Be(Email);

        var laQuePedíaLaPantalla = await cliente.GetAsync("/api/v1/users/me");
        laQuePedíaLaPantalla.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "si algún día existe, el comentario de esta prueba deja de ser cierto y hay que revisarla");
    }
}
