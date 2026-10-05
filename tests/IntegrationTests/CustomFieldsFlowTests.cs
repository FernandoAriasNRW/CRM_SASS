using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Campos personalizados de punta a punta contra la API real.
///
/// Lo que sólo se ve aquí:
///
/// 1. Que el **valor se guarde ya normalizado**. La validación está probada aparte, pero que lo
///    que llega a la base sea de verdad la forma canónica —y no lo que escribió el usuario— sólo
///    se comprueba leyéndolo de vuelta.
/// 2. Que **todas las definiciones lleguen al formulario**, tengan valor o no: si sólo llegaran
///    las rellenas, un campo recién creado no aparecería nunca y nadie podría rellenarlo.
/// 3. Que al **borrar un campo se lleve sus valores**, en lugar de dejar respuestas a una
///    pregunta que ya nadie hace.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CustomFieldsFlowTests(CrmApiFactory factory)
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

    private static async Task<Guid> DefineAsync(HttpClient client, string name, string type,
        bool required = false, string[]? options = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/custom-fields", new
        {
            name = name,
            type = type,
            targetEntity = "Task",
            isRequired = required,
            options = options,
            position = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> SetValueAsync(HttpClient client, Guid field, Guid entity, string? value)
        => client.PutAsJsonAsync($"/api/v1/custom-fields/values/{field}/{entity}", new { value = value });

    private static async Task<JsonElement> ValuesOfAsync(HttpClient client, Guid entity)
        => await client.GetFromJsonAsync<JsonElement>($"/api/v1/custom-fields/values/Task/{entity}");

    [Fact]
    public async Task A_field_is_defined_and_shows_in_the_listing()
    {
        var client = await AuthenticateAsync();
        var name = $"Cliente {Guid.NewGuid():N}";

        var id = await DefineAsync(client, name, "Text");

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/custom-fields?entity=Task");
        list.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).Should().Contain(id);
    }

    [Fact]
    public async Task Two_fields_with_the_same_name_for_one_entity_are_rejected()
    {
        var client = await AuthenticateAsync();
        var name = $"Repetido {Guid.NewGuid():N}";
        await DefineAsync(client, name, "Text");

        var second = await client.PostAsJsonAsync("/api/v1/custom-fields", new
        {
            name = name, type = "Text", targetEntity = "Task", isRequired = false, options = (string[]?)null, position = 0
        });

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).Should().Contain("Ya hay un campo");
    }

    [Fact]
    public async Task The_number_reaches_the_database_normalized()
    {
        var client = await AuthenticateAsync();
        var field = await DefineAsync(client, $"Importe {Guid.NewGuid():N}", "Number");
        var entity = Guid.NewGuid();

        // Se escribe con coma decimal, como en español.
        (await SetValueAsync(client, field, entity, "1234,56")).StatusCode.Should().Be(HttpStatusCode.OK);

        var values = await ValuesOfAsync(client, entity);
        var saved = values.EnumerateArray().Single(v => v.GetProperty("definitionId").GetGuid() == field);

        saved.GetProperty("value").GetString().Should().Be("1234.56", "se guarda con punto para poder ordenar y sumar");
    }

    [Fact]
    public async Task A_value_that_does_not_fit_the_type_is_rejected()
    {
        var client = await AuthenticateAsync();
        var field = await DefineAsync(client, $"Fecha {Guid.NewGuid():N}", "Date");

        var response = await SetValueAsync(client, field, Guid.NewGuid(), "el martes");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("fecha");
    }

    [Fact]
    public async Task Multi_select_is_saved_in_definition_order()
    {
        var client = await AuthenticateAsync();
        var field = await DefineAsync(client, $"Colores {Guid.NewGuid():N}", "MultiSelect",
            options: ["Rojo", "Verde", "Azul"]);
        var entity = Guid.NewGuid();

        await SetValueAsync(client, field, entity, "Azul\nRojo");

        var values = await ValuesOfAsync(client, entity);
        values.EnumerateArray().Single(v => v.GetProperty("definitionId").GetGuid() == field)
            .GetProperty("value").GetString().Should().Be("Rojo\nAzul");
    }

    [Fact]
    public async Task Fields_without_value_still_reach_the_form()
    {
        var client = await AuthenticateAsync();
        var field = await DefineAsync(client, $"Sin rellenar {Guid.NewGuid():N}", "Text");
        var entity = Guid.NewGuid();

        var values = await ValuesOfAsync(client, entity);

        var theField = values.EnumerateArray().SingleOrDefault(v => v.GetProperty("definitionId").GetGuid() == field);
        theField.ValueKind.Should().NotBe(JsonValueKind.Undefined, "un campo nuevo tiene que poder rellenarse");
        theField.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Saving_the_same_field_twice_updates_instead_of_duplicating()
    {
        var client = await AuthenticateAsync();
        var field = await DefineAsync(client, $"Una vez {Guid.NewGuid():N}", "Text");
        var entity = Guid.NewGuid();

        await SetValueAsync(client, field, entity, "primero");
        await SetValueAsync(client, field, entity, "segundo");

        var values = await ValuesOfAsync(client, entity);
        values.EnumerateArray().Count(v => v.GetProperty("definitionId").GetGuid() == field).Should().Be(1);
        values.EnumerateArray().Single(v => v.GetProperty("definitionId").GetGuid() == field)
            .GetProperty("value").GetString().Should().Be("segundo");
    }

    [Fact]
    public async Task Deleting_the_field_removes_its_values()
    {
        var client = await AuthenticateAsync();
        var field = await DefineAsync(client, $"Efímero {Guid.NewGuid():N}", "Text");
        var entity = Guid.NewGuid();
        await SetValueAsync(client, field, entity, "algo");

        var deleted = await client.DeleteAsync($"/api/v1/custom-fields/{field}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var values = await ValuesOfAsync(client, entity);
        values.EnumerateArray().Should().NotContain(v => v.GetProperty("definitionId").GetGuid() == field);
    }

    [Fact]
    public async Task A_required_field_cannot_be_left_empty()
    {
        var client = await AuthenticateAsync();
        var field = await DefineAsync(client, $"Obligatorio {Guid.NewGuid():N}", "Text", required: true);

        var response = await SetValueAsync(client, field, Guid.NewGuid(), "   ");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("obligatorio");
    }

    [Fact]
    public async Task Renaming_a_field_keeps_its_saved_values()
    {
        var client = await AuthenticateAsync();
        var field = await DefineAsync(client, $"Antes {Guid.NewGuid():N}", "Text");
        var entity = Guid.NewGuid();
        await SetValueAsync(client, field, entity, "conservado");

        var newName = $"Después {Guid.NewGuid():N}";
        var updated = await client.PutAsJsonAsync($"/api/v1/custom-fields/{field}", new
        {
            name = newName, isRequired = false, options = (string[]?)null, position = 1
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var values = await ValuesOfAsync(client, entity);
        var theField = values.EnumerateArray().Single(v => v.GetProperty("definitionId").GetGuid() == field);
        theField.GetProperty("name").GetString().Should().Be(newName);
        theField.GetProperty("value").GetString().Should().Be("conservado");
    }
}
