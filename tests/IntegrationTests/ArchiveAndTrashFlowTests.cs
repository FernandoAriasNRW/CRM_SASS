using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que archivar y borrar hagan lo que dicen, y —sobre todo— que lo escondido siga escondido.
///
/// El plan de la Fase 5 avisaba de que el archivado «es la parte que se rompe en silencio: una
/// consulta que se olvide de filtrar enseña archivado como si estuviera vivo». Por eso la
/// comprobación que importa no es «el endpoint responde 204», sino **«desapareció de la lista y
/// aparece en la del archivo»**.
///
/// Todo lo que se archiva o se borra aquí se deja como estaba al terminar. Estas pruebas
/// comparten inquilino con las demás de la colección, y una tarea que se quedara en la papelera
/// cambiaría las cuentas de otra prueba sin que su fallo apuntara aquí. Ya pasó una vez, con
/// unas reglas de automatización sin condiciones que cambiaban la prioridad de todo lo que otras
/// pruebas creaban: pasaban solas y fallaban juntas.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ArchiveAndTrashFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<(int Total, Guid[] Ids)> ListAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();

        return (body.GetProperty("totalCount").GetInt32(), ids);
    }

    /// <summary>Un ticket cualquiera del sembrador, para trastear con él y devolverlo a su sitio.</summary>
    private static async Task<Guid> ATicketAsync(HttpClient client)
    {
        var (_, ids) = await ListAsync(client, "/api/v1/tickets?pageSize=1");
        ids.Should().NotBeEmpty("el sembrador crea tickets; sin ellos esto no comprueba nada");
        return ids[0];
    }

    #region Archivado

    /// <summary>
    /// La prueba que justifica que el filtro sea global y no un <c>Where</c> en cada consulta.
    ///
    /// Si el archivado se hubiera implementado consulta a consulta, ésta pasaría en el listado
    /// —donde alguien se acordó— y fallaría en cualquier otro sitio donde se olvidara.
    /// </summary>
    [Fact]
    public async Task An_archived_ticket_disappears_from_the_normal_list()
    {
        var client = await AuthenticateAsync();
        var ticketId = await ATicketAsync(client);

        try
        {
            var (before, _) = await ListAsync(client, "/api/v1/tickets?pageSize=1");

            (await client.PostAsync($"/api/v1/tickets/{ticketId}/archive", null))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (after, _) = await ListAsync(client, "/api/v1/tickets?pageSize=1");

            after.Should().Be(before - 1, "archivar saca el ticket de la lista de siempre");

            var (_, archived) = await ListAsync(client, "/api/v1/tickets?pageSize=100&filter=archived");
            archived.Should().Contain(ticketId, "y lo pone en el archivo, que es donde se va a buscar");
        }
        finally
        {
            await client.PostAsync($"/api/v1/tickets/{ticketId}/unarchive", null);
        }
    }

    [Fact]
    public async Task Unarchiving_returns_it_to_the_list()
    {
        var client = await AuthenticateAsync();
        var ticketId = await ATicketAsync(client);

        await client.PostAsync($"/api/v1/tickets/{ticketId}/archive", null);
        await client.PostAsync($"/api/v1/tickets/{ticketId}/unarchive", null);

        var (_, ids) = await ListAsync(client, "/api/v1/tickets?pageSize=200");
        ids.Should().Contain(ticketId);
    }

    /// <summary>
    /// El archivo enseña **sólo** lo archivado. Si enseñara todo, sería otra entrada de menú que
    /// promete y devuelve la lista de siempre, que es el fallo que toda la Fase 5A viene a
    /// arreglar.
    /// </summary>
    [Fact]
    public async Task The_archive_is_not_the_whole_list()
    {
        var client = await AuthenticateAsync();

        var (all, _) = await ListAsync(client, "/api/v1/tickets?pageSize=1");
        var (archived, _) = await ListAsync(client, "/api/v1/tickets?pageSize=1&filter=archived");

        all.Should().BeGreaterThan(0);
        archived.Should().BeLessThan(all);
    }

    /// <summary>Archivar dos veces no es un error: es el estado que se pedía.</summary>
    [Fact]
    public async Task Archiving_twice_leaves_the_same_state()
    {
        var client = await AuthenticateAsync();
        var ticketId = await ATicketAsync(client);

        try
        {
            await client.PostAsync($"/api/v1/tickets/{ticketId}/archive", null);

            (await client.PostAsync($"/api/v1/tickets/{ticketId}/archive", null))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (count, _) = await ListAsync(client, "/api/v1/tickets?pageSize=200&filter=archived");
            count.Should().BeGreaterThan(0);
        }
        finally
        {
            await client.PostAsync($"/api/v1/tickets/{ticketId}/unarchive", null);
        }
    }

    #endregion

    #region Papelera

    /// <summary>
    /// El borrado que no borraba.
    ///
    /// <c>DELETE /tickets/{id}</c> leía el ticket, comprobaba que existía y devolvía 204 **sin
    /// tocar nada**: la pantalla decía «borrado» y el ticket seguía ahí al recargar. El de tareas
    /// hacía lo mismo por otro camino —cargaba la tarea y la volvía a guardar sin cambios—.
    /// </summary>
    [Fact]
    public async Task Deleting_a_ticket_really_removes_it_from_the_list()
    {
        var client = await AuthenticateAsync();
        var ticketId = await ATicketAsync(client);

        try
        {
            (await client.DeleteAsync($"/api/v1/tickets/{ticketId}"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (_, visible) = await ListAsync(client, "/api/v1/tickets?pageSize=200");
            visible.Should().NotContain(ticketId, "antes esto devolvía 204 y no borraba nada");

            var (_, trash) = await ListAsync(client, "/api/v1/tickets?pageSize=200&filter=trash");
            trash.Should().Contain(ticketId, "borrar es mandar a la papelera, no evaporar");
        }
        finally
        {
            await client.PostAsync($"/api/v1/tickets/{ticketId}/restore", null);
        }
    }

    [Fact]
    public async Task Restoring_returns_it_to_the_list()
    {
        var client = await AuthenticateAsync();
        var ticketId = await ATicketAsync(client);

        await client.DeleteAsync($"/api/v1/tickets/{ticketId}");

        (await client.PostAsync($"/api/v1/tickets/{ticketId}/restore", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (_, ids) = await ListAsync(client, "/api/v1/tickets?pageSize=200");
        ids.Should().Contain(ticketId);
    }

    /// <summary>
    /// Archivar y borrar no se pisan: lo que estaba archivado sigue archivado al salir de la
    /// papelera. Con una sola columna para las dos cosas, restaurar lo habría devuelto a la lista
    /// principal, que no es donde su dueño lo dejó.
    /// </summary>
    [Fact]
    public async Task Restoring_something_archived_keeps_it_archived()
    {
        var client = await AuthenticateAsync();
        var ticketId = await ATicketAsync(client);

        try
        {
            await client.PostAsync($"/api/v1/tickets/{ticketId}/archive", null);
            await client.DeleteAsync($"/api/v1/tickets/{ticketId}");
            await client.PostAsync($"/api/v1/tickets/{ticketId}/restore", null);

            var (_, visible) = await ListAsync(client, "/api/v1/tickets?pageSize=200");
            visible.Should().NotContain(ticketId, "sigue archivado, así que no vuelve a la lista normal");

            var (_, archived) = await ListAsync(client, "/api/v1/tickets?pageSize=200&filter=archived");
            archived.Should().Contain(ticketId);
        }
        finally
        {
            await client.PostAsync($"/api/v1/tickets/{ticketId}/unarchive", null);
        }
    }

    /// <summary>
    /// Una tarea borrada tampoco vuelve por la puerta de atrás.
    /// </summary>
    [Fact]
    public async Task A_deleted_task_leaves_the_list_and_returns_when_restored()
    {
        var client = await AuthenticateAsync();

        var (_, ids) = await ListAsync(client, "/api/v1/tasks?pageSize=1");
        ids.Should().NotBeEmpty("el sembrador crea tareas");
        var taskId = ids[0];

        try
        {
            (await client.DeleteAsync($"/api/v1/tasks/{taskId}"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (_, visible) = await ListAsync(client, "/api/v1/tasks?pageSize=200");
            visible.Should().NotContain(taskId,
                "el handler cargaba la tarea, la volvía a guardar sin tocarla y devolvía éxito");

            var (_, trash) = await ListAsync(client, "/api/v1/tasks?pageSize=200&filter=trash");
            trash.Should().Contain(taskId);
        }
        finally
        {
            await client.PostAsync($"/api/v1/tasks/{taskId}/restore", null);
        }
    }

    #endregion

    #region El menú entero

    /// <summary>
    /// Las entradas del menú responden en los tres módulos que lo llevan.
    ///
    /// Es una comprobación de contrato, no de contenido: lo que vigila es que ningún módulo se
    /// quede sin entender un filtro que el panel ofrece. Cuando eso pasa, el módulo no falla:
    /// devuelve la lista entera, que es peor.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/tickets")]
    [InlineData("/api/v1/tasks")]
    [InlineData("/api/v1/projects")]
    public async Task All_menu_entries_are_understood_by_the_three_modules(string path)
    {
        var client = await AuthenticateAsync();

        foreach (var filter in new[] { "mine", "created", "favorites", "shared", "private", "archived", "trash" })
        {
            var response = await client.GetAsync($"{path}?pageSize=1&filter={filter}");

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"«{filter}» se ofrece en el panel de navegación, así que {path} tiene que entenderlo");
        }
    }

    #endregion
}
