using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El disparador por vencimiento, la acción de avisar y el registro de ejecuciones.
///
/// Lo que se comprueba aquí y no en las unitarias es que **lo nuevo llega hasta la API**: que el
/// vocabulario lo anuncia, que las reglas se pueden guardar y que el historial responde. La
/// lógica —comparar días, cruzar la medianoche, no repetirse— está probada aparte y sin base de
/// datos, que es donde se puede recorrer la combinatoria.
///
/// **Toda regla de aquí va acotada a un proyecto propio, y no es manía.** La colección comparte
/// un inquilino, así que una automatización activa sin condiciones sobre «tarea creada» se
/// aplica a las tareas que crean *las demás pruebas*. Se escribieron así al principio y
/// rompieron cuatro pruebas de prioridades que no tenían nada que ver: pasaban por separado y
/// fallaban juntas, que es la peor forma de fallar. Una regla es configuración que queda
/// encendida, no un dato que se limpia solo.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TimeBasedAutomationsFlowTests(CrmApiFactory factory)
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

    /// <summary>Los nombres son únicos por inquilino y la colección comparte uno.</summary>
    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString()[..8]}";

    private static Task<HttpResponseMessage> DefineAsync(
        HttpClient client, string name, string trigger, object[] conditions, object[] actions) =>
        client.PostAsJsonAsync("/api/v1/automations", new { name = name, trigger = trigger, conditions = conditions, actions = actions });

    /// <summary>
    /// El vocabulario se sirve, no se repite en el cliente. Si la interfaz ofreciera opciones que
    /// el servidor no conoce, pasaría lo de los informes: media pantalla sin funcionar.
    /// </summary>
    [Fact]
    public async Task The_vocabulary_announces_the_new_options()
    {
        var client = await AuthenticateAsync();

        var vocabulary = await client.GetFromJsonAsync<JsonElement>("/api/v1/automations/vocabulary");

        static string[] ListOf(JsonElement e, string name) =>
            e.GetProperty(name).EnumerateArray().Select(x => x.GetString()!).ToArray();

        ListOf(vocabulary, "triggers").Should().Contain("TaskDueSoon");
        ListOf(vocabulary, "fields").Should().Contain("DaysUntilDue");
        ListOf(vocabulary, "operators").Should().Contain(["LessOrEqual", "GreaterOrEqual"]);
        ListOf(vocabulary, "actions").Should().Contain("Notify");

        // Los metadatos que la interfaz necesita para no ofrecer combinaciones que el dominio
        // rechaza. Servidos también, por el mismo motivo.
        ListOf(vocabulary, "numericFields").Should().Equal("DaysUntilDue");
        ListOf(vocabulary, "numericOperators").Should().BeEquivalentTo(["LessOrEqual", "GreaterOrEqual"]);
        ListOf(vocabulary, "timeTriggers").Should().Equal("TaskDueSoon");
        vocabulary.GetProperty("assigneeRecipient").GetString().Should().Be("Assignee");
    }

    /// <summary>La automatización canónica de este tipo de producto, de punta a punta.</summary>
    [Fact]
    public async Task Notifying_the_assignee_two_days_before_can_be_configured()
    {
        var client = await AuthenticateAsync();

        var response = await DefineAsync(client, Unique("Avisar antes de vencer"), "TaskDueSoon",
            conditions: [new { field = "DaysUntilDue", @operator = "LessOrEqual", value = "2" }],
            actions: [new { type = "Notify", value = "Assignee" }]);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Raising_the_priority_of_overdue_tasks_can_be_configured()
    {
        var client = await AuthenticateAsync();

        // Días negativos: ya venció. Es la forma de expresar «lleva retraso».
        var response = await DefineAsync(client, Unique("Urgente si lleva retraso"), "TaskDueSoon",
            conditions: [new { field = "DaysUntilDue", @operator = "LessOrEqual", value = "-1" }],
            actions: [new { type = "ChangePriority", value = "Urgent" }]);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Comparar «Estado» con «menor o igual» no tiene sentido y se rechaza al guardar, en vez de
    /// caer en la comparación alfabética que daría una regla que salta cuando no debe.
    /// </summary>
    [Fact]
    public async Task A_numeric_operator_on_a_text_field_is_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await DefineAsync(client, Unique("Sin sentido"), "TaskStatusChanged",
            conditions: [new { field = "Status", @operator = "LessOrEqual", value = "Done" }],
            actions: [new { type = "ChangePriority", value = "High" }]);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_numeric_field_compared_with_text_is_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await DefineAsync(client, Unique("Pronto"), "TaskDueSoon",
            conditions: [new { field = "DaysUntilDue", @operator = "EqualTo", value = "pronto" }],
            actions: [new { type = "ChangePriority", value = "High" }]);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #region El registro de ejecuciones

    /// <summary>
    /// Una regla recién creada no tiene historial, y eso es una lista vacía, no un 404: quien
    /// abre el detalle necesita distinguir «no ha pasado nada» de «esto no existe».
    /// </summary>
    [Fact]
    public async Task A_new_rule_has_an_empty_history()
    {
        var client = await AuthenticateAsync();

        // Acotada a un proyecto inexistente. Ver el comentario de la clase: una regla activa sin
        // condiciones en el inquilino compartido cambiaría las tareas de las demás pruebas.
        var creation = await DefineAsync(client, Unique("Sin estrenar"), "TaskCreated",
            conditions: [new { field = "ProjectId", @operator = "EqualTo", value = Guid.NewGuid().ToString() }],
            actions: [new { type = "ChangePriority", value = "High" }]);
        var id = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await client.GetAsync($"/api/v1/automations/{id}/executions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Should().BeEmpty();
    }

    /// <summary>
    /// El caso que el contador de la regla no sabía distinguir: la regla salta, la condición no
    /// se cumple y no pasa nada. Sin este registro, quien la configuró ve el contador a cero y
    /// concluye que el disparador está roto, cuando lo que falla es su condición.
    /// </summary>
    [Fact]
    public async Task It_is_recorded_when_the_rule_fires_and_the_condition_does_not_match()
    {
        var client = await AuthenticateAsync();

        // Condición imposible: ningún proyecto tiene ese identificador.
        var creation = await DefineAsync(client, Unique("Nunca se cumple"), "TaskCreated",
            conditions: [new { field = "ProjectId", @operator = "EqualTo", value = Guid.NewGuid().ToString() }],
            actions: [new { type = "ChangePriority", value = "High" }]);
        var id = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Crear una tarea dispara TareaCreada.
        var task = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = await TestProjects.CreateAsync(client),
            title = "Tarea que dispara la regla",
            description = "Para ver qué anota el registro",
            assigneeId = Guid.Empty,
            estimatedHours = 1,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
        });
        task.EnsureSuccessStatusCode();

        var history = await client.GetFromJsonAsync<JsonElement>($"/api/v1/automations/{id}/executions");

        history.EnumerateArray().Should().NotBeEmpty("la regla saltó, aunque no hiciera nada");
        history.EnumerateArray().First().GetProperty("outcome").GetString()
            .Should().Be("ConditionsNotMet",
                "es exactamente la información que el contador de la regla no podía dar");
    }

    /// <summary>Y cuando sí se aplica, se anota como aplicada.</summary>
    [Fact]
    public async Task It_is_recorded_when_the_rule_applies()
    {
        var client = await AuthenticateAsync();

        // La regla se acota a un proyecto que sólo usa esta prueba. Ver el comentario de la
        // clase: una regla activa sin condiciones alcanzaría a las tareas de las demás.
        var myProject = await TestProjects.CreateAsync(client);

        var creation = await DefineAsync(client, Unique("Urgente en mi proyecto"), "TaskCreated",
            conditions: [new { field = "ProjectId", @operator = "EqualTo", value = myProject.ToString() }],
            actions: [new { type = "ChangePriority", value = "Urgent" }]);
        var id = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var task = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = myProject,
            title = "Tarea que se vuelve urgente",
            description = "La regla se aplica porque el proyecto coincide",
            assigneeId = Guid.Empty,
            estimatedHours = 1,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
        });
        var taskId = (await task.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var history = await client.GetFromJsonAsync<JsonElement>($"/api/v1/automations/{id}/executions");

        var mia = history.EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("entityId").GetGuid() == taskId);

        mia.ValueKind.Should().NotBe(JsonValueKind.Undefined, "la ejecución sobre esa tarea tiene que constar");
        mia.GetProperty("outcome").GetString().Should().Be("Applied");

        // Y la acción ocurrió de verdad, no sólo se anotó.
        var reloaded = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{taskId}");
        var priority = reloaded.GetProperty("priority");
        var value = priority.ValueKind == JsonValueKind.Object
            ? priority.GetProperty("value").GetString()
            : priority.GetString();

        value.Should().Be("Urgent", "un registro que dice «aplicada» sin haber cambiado nada sería la peor mentira de todas");
    }

    #endregion
}
