using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Los campos calculados, de punta a punta contra la API real.
///
/// El motor tiene sus propias pruebas unitarias, exhaustivas y sin base de datos. Éstas
/// comprueban lo otro: que el cálculo **llega hasta quien lo pide**. Es la distinción que ya
/// costó cara con los comentarios —la interfaz llamaba a un endpoint que no existía y nadie lo
/// vio— y con el PATCH que respondía 200 sin guardar.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ComputedFieldsFlowTests(CrmApiFactory factory)
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

    /// <summary>
    /// Los nombres llevan un sufijo único porque las definiciones son del inquilino y la
    /// colección de pruebas comparte uno. Sin esto, la segunda prueba chocaría con el nombre
    /// repetido de la primera y el fallo hablaría de otra cosa.
    /// </summary>
    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString()[..8]}";

    private static async Task<Guid> DefineAsync(
        HttpClient client, string name, string type, string? formula = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/custom-fields", new
        {
            name = name,
            type = type,
            targetEntity = "Task",
            isRequired = false,
            options = (string[]?)null,
            position = 0,
            formula,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> TryDefineAsync(
        HttpClient client, string name, string type, string? formula) =>
        client.PostAsJsonAsync("/api/v1/custom-fields", new
        {
            name = name, type = type, targetEntity = "Task", isRequired = false,
            options = (string[]?)null, position = 0, formula,
        });

    private static async Task SetValueAsync(HttpClient client, Guid field, Guid entity, string value)
    {
        var response = await client.PutAsJsonAsync(
            $"/api/v1/custom-fields/values/{field}/{entity}", new { value = value });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> FieldOfAsync(HttpClient client, Guid entity, Guid field)
    {
        var all = await client.GetFromJsonAsync<JsonElement>($"/api/v1/custom-fields/values/Task/{entity}");

        return all.EnumerateArray().Single(c => c.GetProperty("definitionId").GetGuid() == field);
    }

    /// <summary>Lo básico: se define, se rellenan los ingredientes y el total sale calculado.</summary>
    [Fact]
    public async Task A_computed_field_returns_its_formula_result()
    {
        var client = await AuthenticateAsync();
        var task = Guid.NewGuid();

        var hours = Unique("Horas");
        var price = Unique("Precio");

        var hoursId = await DefineAsync(client, hours, "Number");
        var priceId = await DefineAsync(client, price, "Number");
        var idTotal = await DefineAsync(client, Unique("Total"), "Formula", $"[{hours}] * [{price}]");

        await SetValueAsync(client, hoursId, task, "8");
        await SetValueAsync(client, priceId, task, "50");

        var total = await FieldOfAsync(client, task, idTotal);

        total.GetProperty("value").GetString().Should().Be("400");
        total.GetProperty("type").GetString().Should().Be("Formula");
    }

    /// <summary>
    /// La decisión que más se nota: un ingrediente sin rellenar deja el total sin valor, no en
    /// cero. Un total que dice «400» con la mitad de sus sumandos en blanco se lee como un dato.
    /// </summary>
    [Fact]
    public async Task If_an_input_is_missing_the_total_is_not_invented()
    {
        var client = await AuthenticateAsync();
        var task = Guid.NewGuid();

        var hours = Unique("Horas");
        var price = Unique("Precio");

        var hoursId = await DefineAsync(client, hours, "Number");
        await DefineAsync(client, price, "Number");
        var idTotal = await DefineAsync(client, Unique("Total"), "Formula", $"[{hours}] * [{price}]");

        await SetValueAsync(client, hoursId, task, "8");   // el precio se queda en blanco

        var total = await FieldOfAsync(client, task, idTotal);

        total.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
        total.GetProperty("error").ValueKind.Should().Be(JsonValueKind.Null,
            "faltar un dato es normal, no un error de la fórmula");
    }

    /// <summary>
    /// Una fórmula puede usar el resultado de otra, y para eso hay que calcularlas en orden de
    /// dependencia y no en el de la pantalla.
    /// </summary>
    [Fact]
    public async Task A_formula_can_build_on_another()
    {
        var client = await AuthenticateAsync();
        var task = Guid.NewGuid();

        var hours = Unique("Horas");
        var total = Unique("Total");
        var withVat = Unique("Con IVA");

        var hoursId = await DefineAsync(client, hours, "Number");
        await DefineAsync(client, total, "Formula", $"[{hours}] * 100");
        var withVatId = await DefineAsync(client, withVat, "Formula", $"[{total}] * 1,21");

        await SetValueAsync(client, hoursId, task, "2");

        var result = await FieldOfAsync(client, task, withVatId);

        result.GetProperty("value").GetString().Should().Be("242");
    }

    /// <summary>Cambiar un ingrediente cambia el resultado sin tocar nada más.</summary>
    [Fact]
    public async Task The_result_follows_the_value_it_depends_on()
    {
        var client = await AuthenticateAsync();
        var task = Guid.NewGuid();

        var hours = Unique("Horas");
        var hoursId = await DefineAsync(client, hours, "Number");
        var doubleId = await DefineAsync(client, Unique("Doble"), "Formula", $"[{hours}] * 2");

        await SetValueAsync(client, hoursId, task, "10");
        (await FieldOfAsync(client, task, doubleId)).GetProperty("value").GetString().Should().Be("20");

        await SetValueAsync(client, hoursId, task, "21");
        (await FieldOfAsync(client, task, doubleId)).GetProperty("value").GetString().Should().Be("42",
            "el valor se calcula al leerlo, así que no puede quedarse desfasado");
    }

    [Fact]
    public async Task A_computed_field_cannot_be_filled_by_hand()
    {
        var client = await AuthenticateAsync();

        var hours = Unique("Horas");
        await DefineAsync(client, hours, "Number");
        var idTotal = await DefineAsync(client, Unique("Total"), "Formula", $"[{hours}] * 2");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/custom-fields/values/{idTotal}/{Guid.NewGuid()}", new { value = "999" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "aceptarlo y luego ignorarlo al leer sería peor: el valor desaparecería sin explicación");
    }

    #region Lo que se rechaza al definirlo

    [Fact]
    public async Task A_malformed_formula_is_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await TryDefineAsync(client, Unique("Roto"), "Formula", "2 * (3 + ");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_computed_field_without_formula_is_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await TryDefineAsync(client, Unique("Sin formula"), "Formula", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_formula_pointing_to_a_missing_field_is_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await TryDefineAsync(
            client, Unique("Fantasma"), "Formula", "[Campo Que No Existe] + 1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Campo Que No Existe");
    }

    /// <summary>
    /// Sumar un campo de texto no tiene sentido, y dejarlo pasar haría que la fórmula funcionara
    /// o no según lo que alguien tecleara ese día en un campo libre.
    /// </summary>
    [Fact]
    public async Task A_formula_cannot_use_a_text_field()
    {
        var client = await AuthenticateAsync();

        var note = Unique("Nota");
        await DefineAsync(client, note, "Text");

        var response = await TryDefineAsync(client, Unique("Cuenta"), "Formula", $"[{note}] + 1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// El ciclo es lo que de verdad importa detener: sin esta comprobación, abrir una tarea con
    /// esas dos fórmulas agota la pila y tira la petición.
    /// </summary>
    [Fact]
    public async Task A_formula_closing_a_cycle_is_rejected()
    {
        var client = await AuthenticateAsync();

        var a = Unique("A");
        var b = Unique("B");

        var hours = Unique("Horas");
        await DefineAsync(client, hours, "Number");

        // A se apoya en algo neutro; luego B se apoya en A; y entonces se intenta que A se
        // apoye en B, lo que cerraría A → B → A.
        var idA = await DefineAsync(client, a, "Formula", $"[{hours}] + 1");
        await DefineAsync(client, b, "Formula", $"[{a}] * 2");

        var response = await client.PutAsJsonAsync($"/api/v1/custom-fields/{idA}", new
        {
            name = a,
            isRequired = false,
            options = (string[]?)null,
            position = 0,
            formula = $"[{b}] + 1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("sí misma");
    }

    [Fact]
    public async Task A_self_referencing_formula_is_rejected()
    {
        var client = await AuthenticateAsync();
        var name = Unique("Recursivo");

        var response = await TryDefineAsync(client, name, "Formula", $"[{name}] + 1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}
