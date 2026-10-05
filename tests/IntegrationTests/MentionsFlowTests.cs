using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Las menciones dentro de un documento, y la vuelta: que la tarea sepa quién habla de ella.
///
/// <b>La vuelta es el diferencial.</b> El plan lo decía así: «mencionar un ticket dentro de un
/// documento y que el ticket muestre el documento es algo que ClickUp hace a medias». La ida
/// —escribir la mención y que quede un enlace— la resuelve el editor. La vuelta no se puede
/// resolver leyendo el documento: habría que abrir todos los del inquilino y buscar dentro.
///
/// Y por eso la prueba que importa es la que **escribe una mención por la API y luego pregunta
/// desde el otro lado**. Es la misma forma que la de favoritos: comprueba la unión, que es donde
/// estas cosas se rompen sin dar error.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MentionsFlowTests(CrmApiFactory factory)
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

    /// <summary>Un documento con una página, para escribir dentro.</summary>
    private static async Task<(Guid DocumentId, Guid PageId)> CreateDocumentAsync(HttpClient client)
    {
        var creation = await client.PostAsJsonAsync("/api/v1/docs", new
        {
            Title = $"Notas {Guid.NewGuid():N}"[..24],
            Description = "Para probar menciones",
            Type = 0,
            TeamId = (Guid?)null,
            ProjectId = (Guid?)null,
            InitialContent = (string?)null
        });

        creation.StatusCode.Should().Match(c => c == HttpStatusCode.Created || c == HttpStatusCode.OK,
            await creation.Content.ReadAsStringAsync());

        // El endpoint devuelve el identificador **como una cadena suelta**, no envuelto en un
        // objeto. Es lo que hace de verdad, así que es lo que se lee: una prueba que asume otra
        // forma comprueba una API imaginaria.
        var documentId = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetGuid();

        // Un documento nace con una página, que es donde se escribe.
        var pages = await client.GetFromJsonAsync<JsonElement>($"/api/v1/docs/{documentId}/pages");
        var first = pages.EnumerateArray().First();

        return (documentId, first.GetProperty("id").GetGuid());
    }

    /// <summary>Escribe el contenido de una página, tal como lo manda el editor.</summary>
    private static async Task SaveAsync(HttpClient client, Guid pageId, string title, string html)
    {
        var response = await client.PutAsJsonAsync($"/api/v1/docs/pages/{pageId}",
            new { Title = title, Content = html });

        response.StatusCode.Should().Match(c => c == HttpStatusCode.OK || c == HttpStatusCode.NoContent,
            await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> GetMentioningDocumentsAsync(HttpClient client, string type, Guid entityId)
        => await client.GetFromJsonAsync<JsonElement>($"/api/v1/docs/mentions/{type}/{entityId}");

    private static async Task<Guid> ATaskAsync(HttpClient client)
    {
        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/tasks?pageSize=1");
        var items = list.GetProperty("items").EnumerateArray().ToList();

        items.Should().NotBeEmpty("el sembrador crea tareas");
        return items[0].GetProperty("id").GetGuid();
    }

    #region La vuelta: el diferencial

    /// <summary>
    /// La prueba que conecta los dos lados: se menciona una tarea en un documento y la tarea la ve.
    ///
    /// Si el formato que escribe el editor y el que lee el servidor divergieran, esto devolvería
    /// una lista vacía **sin dar ningún error** — la clase de fallo que nadie detecta hasta que
    /// alguien pregunta por qué su documento no aparece en ningún sitio.
    /// </summary>
    [Fact]
    public async Task A_mentioned_task_knows_which_document_mentions_it()
    {
        var client = await AuthenticateAsync();
        var taskId = await ATaskAsync(client);
        var (_, pageId) = await CreateDocumentAsync(client);

        await SaveAsync(client, pageId, "Acta de reunión",
            $"""<p>Se acordó avanzar con <span data-mention-type="Task" data-mention-id="{taskId}">esa tarea</span>.</p>""");

        var who = await GetMentioningDocumentsAsync(client, "Task", taskId);

        // Se busca **esta** página, no se exige ser la única que menciona la tarea. Todas las
        // pruebas de esta clase trabajan sobre la misma tarea del sembrador, así que exigir
        // exclusividad las hacía pasar por separado y fallar juntas — el patrón que ya mordió con
        // las automatizaciones sin condiciones.
        var mention = who.EnumerateArray()
            .Should().ContainSingle(m => m.GetProperty("pageId").GetGuid() == pageId).Subject;

        mention.GetProperty("pageTitle").GetString().Should().Be("Acta de reunión");
        mention.GetProperty("visibleText").GetString().Should().Be("esa tarea");
    }

    /// <summary>
    /// Borrar la mención del texto la borra de verdad.
    ///
    /// Es la razón de que las menciones se reescriban enteras en cada guardado en vez de ir
    /// añadiendo: con un diferencial, lo que no se detecta se queda para siempre y la tarea
    /// seguiría enseñando un documento que ya no habla de ella.
    /// </summary>
    [Fact]
    public async Task Removing_the_mention_from_the_text_removes_it_from_the_task()
    {
        var client = await AuthenticateAsync();
        var taskId = await ATaskAsync(client);
        var (_, pageId) = await CreateDocumentAsync(client);

        await SaveAsync(client, pageId, "Con mención",
            $"""<p><span data-mention-type="Task" data-mention-id="{taskId}">La tarea</span></p>""");

        (await GetMentioningDocumentsAsync(client, "Task", taskId)).EnumerateArray()
            .Should().ContainSingle(m => m.GetProperty("pageId").GetGuid() == pageId);

        await SaveAsync(client, pageId, "Sin mención", "<p>Ya no se habla de nada.</p>");

        (await GetMentioningDocumentsAsync(client, "Task", taskId)).EnumerateArray()
            .Should().NotContain(m => m.GetProperty("pageId").GetGuid() == pageId,
                "la mención se borró del texto, así que la tarea no puede seguir enseñando el documento");
    }

    /// <summary>
    /// El índice se rehace, no se acumula: guardar dos veces no duplica la mención.
    ///
    /// Es lo que pasaría si se insertara sin borrar antes, y la pantalla de la tarea enseñaría el
    /// mismo documento tantas veces como se hubiera guardado.
    /// </summary>
    [Fact]
    public async Task Saving_several_times_does_not_duplicate_the_mention()
    {
        var client = await AuthenticateAsync();
        var taskId = await ATaskAsync(client);
        var (_, pageId) = await CreateDocumentAsync(client);

        var html = $"""<p><span data-mention-type="Task" data-mention-id="{taskId}">Otra vez</span></p>""";

        await SaveAsync(client, pageId, "Uno", html);
        await SaveAsync(client, pageId, "Dos", html);
        await SaveAsync(client, pageId, "Tres", html);

        (await GetMentioningDocumentsAsync(client, "Task", taskId)).EnumerateArray()
            .Count(m => m.GetProperty("pageId").GetGuid() == pageId)
            .Should().Be(1);
    }

    /// <summary>Sin nadie que la mencione, la lista es vacía: no es un error, es la respuesta.</summary>
    [Fact]
    public async Task A_task_without_mentions_returns_an_empty_list()
    {
        var client = await AuthenticateAsync();

        var who = await GetMentioningDocumentsAsync(client, "Task", Guid.NewGuid());

        who.EnumerateArray().Should().BeEmpty();
    }

    #endregion

    #region Qué se puede mencionar

    [Fact]
    public async Task Tickets_and_people_can_be_mentioned_besides_tasks()
    {
        var client = await AuthenticateAsync();
        var (_, pageId) = await CreateDocumentAsync(client);

        var ticketId = Guid.NewGuid();
        var personId = Guid.NewGuid();

        await SaveAsync(client, pageId, "Varias",
            $"""
             <p><span data-mention-type="Ticket" data-mention-id="{ticketId}">Un ticket</span>
             y <span data-mention-type="Person" data-mention-id="{personId}">alguien</span></p>
             """);

        (await GetMentioningDocumentsAsync(client, "Ticket", ticketId)).EnumerateArray().Should().ContainSingle();
        (await GetMentioningDocumentsAsync(client, "Person", personId)).EnumerateArray().Should().ContainSingle();
    }

    /// <summary>
    /// Un tipo que no se puede mencionar se rechaza diciendo cuáles sí.
    ///
    /// La lista es cerrada a propósito: una mención a un tipo que ninguna pantalla enseña es un
    /// dato que no vuelve a ver nadie.
    /// </summary>
    [Fact]
    public async Task A_type_that_cannot_be_mentioned_is_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await client.GetAsync($"/api/v1/docs/mentions/Factura/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var message = await response.Content.ReadAsStringAsync();
        message.Should().Contain("Factura").And.Contain("Task");
    }

    /// <summary>Una mención mal formada no se guarda, y el guardado del texto no falla por ello.</summary>
    [Fact]
    public async Task A_mention_without_id_does_not_block_saving_the_page()
    {
        var client = await AuthenticateAsync();
        var (documentId, pageId) = await CreateDocumentAsync(client);

        await SaveAsync(client, pageId, "Rota",
            """<p><span data-mention-type="Task">Sin identificador</span></p>""");

        // Lo que importa es que el texto de la persona se haya guardado: una mención a medias no
        // puede costarle el contenido.
        var pages = await client.GetFromJsonAsync<JsonElement>($"/api/v1/docs/{documentId}/pages");

        pages.EnumerateArray()
            .Should().Contain(p => p.GetProperty("title").GetString() == "Rota");
    }

    #endregion

    [Fact]
    public async Task Without_authentication_mentions_cannot_be_read()
    {
        var anonymous = factory.CreateClient();

        (await anonymous.GetAsync($"/api/v1/docs/mentions/Task/{Guid.NewGuid()}")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }
}
