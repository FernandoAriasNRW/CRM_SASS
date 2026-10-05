using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Automations.Domain.Entities;
using Automations.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El motor de automatizaciones, de punta a punta contra la API real.
///
/// Aquí está la prueba que de verdad importa del 4D: **se configura una regla, se mueve una
/// tarea y se comprueba que la tarea cambió sola**. Recorre la cadena entera —evento de dominio,
/// puente del host, motor, acción de vuelta sobre WorkItems— y ninguna prueba unitaria puede
/// cubrirla, porque lo que se está probando es precisamente que las piezas están conectadas.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AutomationsFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<(HttpClient client, Guid tenantId)> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        return (client, TenantFromToken(token));
    }

    private static Guid TenantFromToken(string token)
    {
        var body = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var payload = body.PadRight(body.Length + (4 - body.Length % 4) % 4, '=');
        var json = JsonDocument.Parse(Convert.FromBase64String(payload));

        return Guid.Parse(json.RootElement.GetProperty("tenantId").GetString()!);
    }

    private async Task<Guid> CreateTaskAsync(HttpClient client, Guid tenantId, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId = Guid.NewGuid(),
            title = title,
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 2m,
            dueDate = "2026-12-01",
            priority = "Normal"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static object RuleThatLowersPriorityOnDone(string name) => new
    {
        name = name,
        trigger = "TaskStatusChanged",
        conditions = new[] { new { field = "Status", @operator = "EqualTo", value = "Done" } },
        actions = new[] { new { type = "ChangePriority", value = "Low" } },
    };

    /// <summary>
    /// Borra las reglas que haya antes de empezar.
    ///
    /// Las pruebas de esta colección comparten un MySQL, y una automatización activa que dejó
    /// otra prueba **se ejecuta igual**: eso es lo que hace el motor. Sin limpiar, comprobar que
    /// «esta regla no se ejecutó» falla porque se ejecutó otra, y el fallo depende del orden.
    /// </summary>
    private static async Task ClearRulesAsync(HttpClient client)
    {
        var rules = await client.GetFromJsonAsync<JsonElement>("/api/v1/automations");

        foreach (var rule in rules.EnumerateArray())
            await client.DeleteAsync($"/api/v1/automations/{rule.GetProperty("id").GetGuid()}");
    }

    private async Task<Guid> CreateRuleAsync(HttpClient client, object rule)
    {
        var response = await client.PostAsJsonAsync("/api/v1/automations", rule);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> RuleAsync(HttpClient client, Guid id)
    {
        var rules = await client.GetFromJsonAsync<JsonElement>("/api/v1/automations");
        return rules.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task A_rule_runs_by_itself_when_its_trigger_fires()
    {
        var (client, tenantId) = await AuthenticateAsync();
        await ClearRulesAsync(client);
        var ruleId = await CreateRuleAsync(client, RuleThatLowersPriorityOnDone($"Bajar al cerrar {Guid.NewGuid()}"));
        var taskId = await CreateTaskAsync(client, tenantId, "Tarea que se cerrará");

        await client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}", new { status = "Done" });

        var task = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{taskId}");
        task.GetProperty("priority").GetString().Should().Be("Low",
            "la automatización tenía que haber bajado la prioridad al pasar la tarea a Done");

        var rule = await RuleAsync(client, ruleId);
        rule.GetProperty("executionCount").GetInt32().Should().Be(1);
        rule.GetProperty("lastExecutedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    /// <summary>
    /// Es el reverso de la prueba anterior y hace falta: sin ella, una regla que se ejecutara
    /// siempre —ignorando sus condiciones— pasaría la primera igual de bien.
    /// </summary>
    [Fact]
    public async Task A_rule_does_not_run_if_its_conditions_do_not_match()
    {
        var (client, tenantId) = await AuthenticateAsync();
        await ClearRulesAsync(client);
        var ruleId = await CreateRuleAsync(client, RuleThatLowersPriorityOnDone($"Bajar al cerrar {Guid.NewGuid()}"));
        var taskId = await CreateTaskAsync(client, tenantId, "Tarea que sólo avanza");

        await client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}", new { status = "In Progress" });

        var task = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{taskId}");
        task.GetProperty("priority").GetString().Should().Be("Normal");

        (await RuleAsync(client, ruleId)).GetProperty("executionCount").GetInt32().Should().Be(0);
    }

    /// <summary>
    /// Desactivar es la operación que se hace con prisa, cuando una automatización está haciendo
    /// daño. Si la regla siguiera ejecutándose, el botón sería decorativo.
    /// </summary>
    [Fact]
    public async Task A_deactivated_rule_does_not_run()
    {
        var (client, tenantId) = await AuthenticateAsync();
        await ClearRulesAsync(client);
        var ruleId = await CreateRuleAsync(client, RuleThatLowersPriorityOnDone($"Desactivada {Guid.NewGuid()}"));

        var turnOff = await client.PutAsJsonAsync($"/api/v1/automations/{ruleId}/active", new { isActive = false });
        turnOff.StatusCode.Should().Be(HttpStatusCode.OK);

        var taskId = await CreateTaskAsync(client, tenantId, "Tarea con la regla apagada");
        await client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}", new { status = "Done" });

        var task = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{taskId}");
        task.GetProperty("priority").GetString().Should().Be("Normal");
    }

    /// <summary>
    /// Lo que garantiza que las automatizaciones no se encadenen: la acción de una regla emite su
    /// propio evento, y si ese evento disparara otras reglas, dos reglas que se deshacen la una a
    /// la otra se llamarían para siempre.
    /// </summary>
    [Fact]
    public async Task A_rule_actions_do_not_trigger_other_rules()
    {
        var (client, tenantId) = await AuthenticateAsync();
        await ClearRulesAsync(client);

        // La primera pasa la tarea a Done; la segunda reaccionaría a ese Done bajando la
        // prioridad. Si las cadenas existieran, la prioridad acabaría en Low.
        await CreateRuleAsync(client, new
        {
            name = $"Cerrar al revisar {Guid.NewGuid()}",
            trigger = "TaskStatusChanged",
            conditions = new[] { new { field = "Status", @operator = "EqualTo", value = "In Review" } },
            actions = new[] { new { type = "ChangeStatus", value = "Done" } },
        });

        await CreateRuleAsync(client, new
        {
            name = $"Bajar al cerrar {Guid.NewGuid()}",
            trigger = "TaskStatusChanged",
            conditions = new[] { new { field = "Status", @operator = "EqualTo", value = "Done" } },
            actions = new[] { new { type = "ChangePriority", value = "Low" } },
        });

        var taskId = await CreateTaskAsync(client, tenantId, "Tarea que pasa por revisión");
        await client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}", new { status = "In Review" });

        var task = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{taskId}");
        task.GetProperty("status").GetString().Should().Be("Done", "la primera regla sí se ejecutó");
        task.GetProperty("priority").GetString().Should().Be("Normal",
            "la segunda no, porque las acciones de una automatización no disparan otras");
    }

    [Fact]
    public async Task A_rule_without_actions_is_rejected()
    {
        var (client, _) = await AuthenticateAsync();
        await ClearRulesAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/automations", new
        {
            name = $"Sin acciones {Guid.NewGuid()}",
            trigger = "TaskStatusChanged",
            conditions = Array.Empty<object>(),
            actions = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Two_rules_cannot_share_a_name()
    {
        var (client, _) = await AuthenticateAsync();
        await ClearRulesAsync(client);
        var name = $"Repetida {Guid.NewGuid()}";

        await CreateRuleAsync(client, RuleThatLowersPriorityOnDone(name));
        var second = await client.PostAsJsonAsync("/api/v1/automations", RuleThatLowersPriorityOnDone(name));

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_server_serves_the_vocabulary()
    {
        var (client, _) = await AuthenticateAsync();
        await ClearRulesAsync(client);

        var vocabulary = await client.GetFromJsonAsync<JsonElement>("/api/v1/automations/vocabulary");

        // La interfaz construye el formulario con esto. Repetir la lista en el cliente la dejaría
        // desincronizada el día que se añada un disparador.
        vocabulary.GetProperty("triggers").GetArrayLength().Should().BeGreaterThan(0);
        vocabulary.GetProperty("operators").GetArrayLength().Should().BeGreaterThan(0);
        vocabulary.GetProperty("actions").GetArrayLength().Should().BeGreaterThan(0);
    }

    /// <summary>
    /// El vocabulario dice qué campos trae cada disparador, y la interfaz sólo ofrece ésos. Las
    /// claves son los códigos de disparador tal cual: la serialización no las pasa a camelCase.
    /// </summary>
    [Fact]
    public async Task The_vocabulary_serves_the_fields_each_trigger_carries()
    {
        var (client, _) = await AuthenticateAsync();

        var vocabulary = await client.GetFromJsonAsync<JsonElement>("/api/v1/automations/vocabulary");
        var byTrigger = vocabulary.GetProperty("fieldsByTrigger");

        byTrigger.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo(TriggerTypes.All());

        foreach (var trigger in TriggerTypes.All())
        {
            byTrigger.GetProperty(trigger).EnumerateArray().Select(f => f.GetString())
                .Should().Equal(EventFields.ForTrigger(trigger), trigger);
        }

        byTrigger.GetProperty(TriggerTypes.TaskCreated).EnumerateArray()
            .Select(f => f.GetString()).Should().Contain(EventFields.Title)
            .And.NotContain(EventFields.PreviousStatus);
    }

    /// <summary>
    /// Una condición sobre un campo que el disparador no trae —el estado anterior al crear una
    /// tarea— se rechaza al guardar, y también al editar una regla existente para ponérsela.
    /// </summary>
    [Fact]
    public async Task A_condition_on_a_field_the_trigger_does_not_carry_is_rejected()
    {
        var (client, _) = await AuthenticateAsync();
        await ClearRulesAsync(client);

        object Rule(string name) => new
        {
            name = name,
            trigger = TriggerTypes.TaskCreated,
            conditions = new[] { new { field = EventFields.PreviousStatus, @operator = "EqualTo", value = "Done" } },
            actions = new[] { new { type = "ChangePriority", value = "Low" } },
        };

        var onCreate = await client.PostAsJsonAsync("/api/v1/automations", Rule($"Anterior {Guid.NewGuid()}"));

        onCreate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await onCreate.Content.ReadFromJsonAsync<string>()).Should().Contain(AutomationRule.Rules.FieldNotInTrigger);

        var name = $"Editada {Guid.NewGuid()}";
        var id = await CreateRuleAsync(client, RuleThatLowersPriorityOnDone(name));

        var onEdit = await client.PutAsJsonAsync($"/api/v1/automations/{id}", Rule(name));

        onEdit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await onEdit.Content.ReadFromJsonAsync<string>()).Should().Contain(AutomationRule.Rules.FieldNotInTrigger);
        (await RuleAsync(client, id)).GetProperty("trigger").GetString()
            .Should().Be(TriggerTypes.TaskStatusChanged, "el rechazo no deja la regla a medias");
    }

    /// <summary>
    /// El caso medido, de punta a punta: «se crea una tarea» con «el título contiene 8b» antes se
    /// anotaba como condiciones no cumplidas porque el evento no traía el título. Ahora lo trae, y
    /// la regla se aplica sólo a la tarea cuyo título lo contiene.
    /// </summary>
    [Fact]
    public async Task A_task_created_rule_can_look_at_the_title()
    {
        var (client, tenantId) = await AuthenticateAsync();
        await ClearRulesAsync(client);
        var ruleId = await CreateRuleAsync(client, new
        {
            name = $"Título con 8b {Guid.NewGuid()}",
            trigger = TriggerTypes.TaskCreated,
            conditions = new[] { new { field = EventFields.Title, @operator = "Contains", value = "8b" } },
            actions = new[] { new { type = "ChangePriority", value = "Low" } },
        });

        var withTitle = await CreateTaskAsync(client, tenantId, "Revisar el bloque 8b");
        var withoutTitle = await CreateTaskAsync(client, tenantId, "Revisar otra cosa");

        (await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{withTitle}"))
            .GetProperty("priority").GetString().Should().Be("Low");
        (await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{withoutTitle}"))
            .GetProperty("priority").GetString().Should().Be("Normal");
        (await RuleAsync(client, ruleId)).GetProperty("executionCount").GetInt32().Should().Be(1);
    }

    /// <summary>
    /// Un mismo cambio que renombra la tarea y la mueve: el evento de estado tiene que llevar el
    /// título nuevo. Antes el comando aplicaba el estado antes que el título, y la regla habría
    /// mirado el viejo.
    /// </summary>
    [Fact]
    public async Task A_status_rule_sees_the_title_changed_in_the_same_patch()
    {
        var (client, tenantId) = await AuthenticateAsync();
        await ClearRulesAsync(client);
        var ruleId = await CreateRuleAsync(client, new
        {
            name = $"Cerrar con 8b {Guid.NewGuid()}",
            trigger = TriggerTypes.TaskStatusChanged,
            conditions = new[] { new { field = EventFields.Title, @operator = "Contains", value = "8b" } },
            actions = new[] { new { type = "ChangePriority", value = "Low" } },
        });
        var taskId = await CreateTaskAsync(client, tenantId, "Revisar el bloque");

        await client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}", new { title = "Revisar el bloque 8b", status = "Done" });

        (await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{taskId}"))
            .GetProperty("priority").GetString().Should().Be("Low");
        (await RuleAsync(client, ruleId)).GetProperty("executionCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task A_rule_can_be_deleted()
    {
        var (client, _) = await AuthenticateAsync();
        await ClearRulesAsync(client);
        var ruleId = await CreateRuleAsync(client, RuleThatLowersPriorityOnDone($"Para borrar {Guid.NewGuid()}"));

        var deleted = await client.DeleteAsync($"/api/v1/automations/{ruleId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rules = await client.GetFromJsonAsync<JsonElement>("/api/v1/automations");
        rules.EnumerateArray().Should().NotContain(r => r.GetProperty("id").GetGuid() == ruleId);
    }

    [Fact]
    public async Task A_missing_rule_returns_404()
    {
        var (client, _) = await AuthenticateAsync();
        await ClearRulesAsync(client);

        var response = await client.DeleteAsync($"/api/v1/automations/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
