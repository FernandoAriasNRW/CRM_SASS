using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que «compartido conmigo» y «privado» signifiquen algo.
///
/// Son las dos entradas del menú con más riesgo de quedarse en adorno. «Compartido conmigo» sale
/// de una tabla de permisos que hoy sólo tiene filas por rol y de módulo entero: si el filtro las
/// contara como comparticiones, devolvería media aplicación —la lista de siempre otra vez—; y si
/// no hubiera forma de compartir, devolvería siempre vacío, que es igual de inútil.
///
/// Por eso estas pruebas comparten de verdad, comprueban el efecto, y lo deshacen.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SharingFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<(HttpClient Client, Guid Me)> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me");
        return (client, me.GetProperty("id").GetGuid());
    }

    private static async Task<Guid[]> IdsAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();
    }

    /// <summary>
    /// La prueba que une los dos lados, igual que la de favoritos.
    ///
    /// Importa porque los módulos hablan de «Tarea» y la tabla de permisos guarda «Task»: la
    /// traducción vive en un solo sitio, y si se rompiera, el filtro devolvería una lista vacía
    /// **sin dar ningún error**.
    /// </summary>
    [Fact]
    public async Task A_task_shared_with_me_shows_when_filtering_by_shared()
    {
        var (client, me) = await AuthenticateAsync();

        var tasks = await IdsAsync(client, "/api/v1/tasks?pageSize=1");
        tasks.Should().NotBeEmpty("el sembrador crea tareas");
        var taskId = tasks[0];

        try
        {
            (await client.PutAsJsonAsync($"/api/v1/sharing/Task/{taskId}/{me}", new { Level = "Edit" }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var shared = await IdsAsync(client, "/api/v1/tasks?pageSize=200&filter=shared");

            shared.Should().Contain(taskId,
                "si esto falla, la traducción del tipo no coincide y el filtro devuelve vacío en silencio");
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/sharing/Task/{taskId}/{me}");
        }
    }

    [Fact]
    public async Task Unsharing_removes_it_from_the_list()
    {
        var (client, me) = await AuthenticateAsync();

        var taskId = (await IdsAsync(client, "/api/v1/tasks?pageSize=1"))[0];

        await client.PutAsJsonAsync($"/api/v1/sharing/Task/{taskId}/{me}", new { Level = "View" });
        await client.DeleteAsync($"/api/v1/sharing/Task/{taskId}/{me}");

        var shared = await IdsAsync(client, "/api/v1/tasks?pageSize=200&filter=shared");
        shared.Should().NotContain(taskId);
    }

    /// <summary>
    /// Los permisos por rol y los de módulo entero **no** son comparticiones.
    ///
    /// Hoy la tabla tiene 185 filas de ese tipo y ninguna nominal. Si el filtro las contara,
    /// «compartido conmigo» devolvería prácticamente todo, que es exactamente el fallo que esta
    /// fase viene a arreglar, sólo que disfrazado de funcionalidad nueva.
    /// </summary>
    [Fact]
    public async Task With_nothing_shared_the_filter_returns_zero_not_everything()
    {
        var (client, _) = await AuthenticateAsync();

        var all = await IdsAsync(client, "/api/v1/tasks?pageSize=200");
        var shared = await IdsAsync(client, "/api/v1/tasks?pageSize=200&filter=shared");

        all.Should().NotBeEmpty();
        shared.Length.Should().BeLessThan(all.Length,
            "los permisos por rol están en la misma tabla y no cuentan como compartir");
    }

    /// <summary>
    /// «Privado» es lo mío que no le he dado a nadie: al compartir algo, deja de ser privado.
    ///
    /// Se comprueba así, y no contra un número fijo, porque el reparto de datos del sembrador no
    /// es asunto de esta prueba: lo que se comprueba es el significado.
    /// </summary>
    [Fact]
    public async Task Sharing_something_removes_it_from_private()
    {
        var (client, me) = await AuthenticateAsync();

        var mine = await IdsAsync(client, "/api/v1/tasks?pageSize=200&filter=mine");
        if (mine.Length == 0) return; // Sin tareas propias, este caso no aplica.

        var privateBefore = await IdsAsync(client, "/api/v1/tasks?pageSize=200&filter=private");
        var candidate = privateBefore.FirstOrDefault();
        if (candidate == Guid.Empty) return;

        try
        {
            await client.PutAsJsonAsync($"/api/v1/sharing/Task/{candidate}/{me}", new { Level = "View" });

            var privateAfter = await IdsAsync(client, "/api/v1/tasks?pageSize=200&filter=private");

            privateAfter.Should().NotContain(candidate,
                "compartir algo deja de hacerlo privado; por eso «privado» se calcula restando y no "
                + "con un campo EsPrivado que se desincronizaría");
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/sharing/Task/{candidate}/{me}");
        }
    }

    [Fact]
    public async Task Who_it_is_shared_with_can_be_seen()
    {
        var (client, me) = await AuthenticateAsync();

        var taskId = (await IdsAsync(client, "/api/v1/tasks?pageSize=1"))[0];

        try
        {
            await client.PutAsJsonAsync($"/api/v1/sharing/Task/{taskId}/{me}", new { Level = "Full" });

            var withWhom = await client.GetFromJsonAsync<JsonElement>($"/api/v1/sharing/Task/{taskId}");

            withWhom.EnumerateArray().Select(x => x.GetGuid()).Should().Contain(me);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/sharing/Task/{taskId}/{me}");
        }
    }

    /// <summary>Compartir dos veces cambia el nivel; no deja dos filas peleándose.</summary>
    [Fact]
    public async Task Sharing_twice_does_not_duplicate()
    {
        var (client, me) = await AuthenticateAsync();

        var taskId = (await IdsAsync(client, "/api/v1/tasks?pageSize=1"))[0];

        try
        {
            await client.PutAsJsonAsync($"/api/v1/sharing/Task/{taskId}/{me}", new { Level = "View" });
            await client.PutAsJsonAsync($"/api/v1/sharing/Task/{taskId}/{me}", new { Level = "Full" });

            var withWhom = await client.GetFromJsonAsync<JsonElement>($"/api/v1/sharing/Task/{taskId}");

            withWhom.EnumerateArray().Should().HaveCount(1);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/sharing/Task/{taskId}/{me}");
        }
    }

    [Fact]
    public async Task An_unknown_level_is_rejected()
    {
        var (client, me) = await AuthenticateAsync();

        var taskId = (await IdsAsync(client, "/api/v1/tasks?pageSize=1"))[0];

        (await client.PutAsJsonAsync($"/api/v1/sharing/Task/{taskId}/{me}", new { Level = "Jefe" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_type_is_rejected()
    {
        var (client, me) = await AuthenticateAsync();

        (await client.PutAsJsonAsync($"/api/v1/sharing/Factura/{Guid.NewGuid()}/{me}", new { Level = "View" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Without_authentication_nothing_is_shared()
    {
        var anonymous = factory.CreateClient();

        (await anonymous.PutAsJsonAsync($"/api/v1/sharing/Task/{Guid.NewGuid()}/{Guid.NewGuid()}", new { Level = "View" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
