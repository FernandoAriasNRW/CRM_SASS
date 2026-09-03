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
public sealed class CamposCalculadosFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    /// <summary>
    /// Los nombres llevan un sufijo único porque las definiciones son del inquilino y la
    /// colección de pruebas comparte uno. Sin esto, la segunda prueba chocaría con el nombre
    /// repetido de la primera y el fallo hablaría de otra cosa.
    /// </summary>
    private static string Unico(string nombre) => $"{nombre} {Guid.NewGuid().ToString()[..8]}";

    private static async Task<Guid> DefinirAsync(
        HttpClient cliente, string nombre, string tipo, string? formula = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/custom-fields", new
        {
            nombre,
            tipo,
            entidadDestino = "Tarea",
            obligatorio = false,
            opciones = (string[]?)null,
            posicion = 0,
            formula,
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> IntentarDefinirAsync(
        HttpClient cliente, string nombre, string tipo, string? formula) =>
        cliente.PostAsJsonAsync("/api/v1/custom-fields", new
        {
            nombre, tipo, entidadDestino = "Tarea", obligatorio = false,
            opciones = (string[]?)null, posicion = 0, formula,
        });

    private static async Task PonerValorAsync(HttpClient cliente, Guid campo, Guid entidad, string valor)
    {
        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/v1/custom-fields/values/{campo}/{entidad}", new { valor });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> CampoDeAsync(HttpClient cliente, Guid entidad, Guid campo)
    {
        var todos = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/custom-fields/values/Tarea/{entidad}");

        return todos.EnumerateArray().Single(c => c.GetProperty("definitionId").GetGuid() == campo);
    }

    /// <summary>Lo básico: se define, se rellenan los ingredientes y el total sale calculado.</summary>
    [Fact]
    public async Task Un_campo_calculado_devuelve_el_resultado_de_su_formula()
    {
        var cliente = await AutenticarAsync();
        var tarea = Guid.NewGuid();

        var horas = Unico("Horas");
        var precio = Unico("Precio");

        var idHoras = await DefinirAsync(cliente, horas, "Numero");
        var idPrecio = await DefinirAsync(cliente, precio, "Numero");
        var idTotal = await DefinirAsync(cliente, Unico("Total"), "Formula", $"[{horas}] * [{precio}]");

        await PonerValorAsync(cliente, idHoras, tarea, "8");
        await PonerValorAsync(cliente, idPrecio, tarea, "50");

        var total = await CampoDeAsync(cliente, tarea, idTotal);

        total.GetProperty("valor").GetString().Should().Be("400");
        total.GetProperty("tipo").GetString().Should().Be("Formula");
    }

    /// <summary>
    /// La decisión que más se nota: un ingrediente sin rellenar deja el total sin valor, no en
    /// cero. Un total que dice «400» con la mitad de sus sumandos en blanco se lee como un dato.
    /// </summary>
    [Fact]
    public async Task Si_falta_un_ingrediente_el_total_no_se_inventa()
    {
        var cliente = await AutenticarAsync();
        var tarea = Guid.NewGuid();

        var horas = Unico("Horas");
        var precio = Unico("Precio");

        var idHoras = await DefinirAsync(cliente, horas, "Numero");
        await DefinirAsync(cliente, precio, "Numero");
        var idTotal = await DefinirAsync(cliente, Unico("Total"), "Formula", $"[{horas}] * [{precio}]");

        await PonerValorAsync(cliente, idHoras, tarea, "8");   // el precio se queda en blanco

        var total = await CampoDeAsync(cliente, tarea, idTotal);

        total.GetProperty("valor").ValueKind.Should().Be(JsonValueKind.Null);
        total.GetProperty("error").ValueKind.Should().Be(JsonValueKind.Null,
            "faltar un dato es normal, no un error de la fórmula");
    }

    /// <summary>
    /// Una fórmula puede usar el resultado de otra, y para eso hay que calcularlas en orden de
    /// dependencia y no en el de la pantalla.
    /// </summary>
    [Fact]
    public async Task Una_formula_puede_apoyarse_en_otra()
    {
        var cliente = await AutenticarAsync();
        var tarea = Guid.NewGuid();

        var horas = Unico("Horas");
        var total = Unico("Total");
        var conIva = Unico("Con IVA");

        var idHoras = await DefinirAsync(cliente, horas, "Numero");
        await DefinirAsync(cliente, total, "Formula", $"[{horas}] * 100");
        var idConIva = await DefinirAsync(cliente, conIva, "Formula", $"[{total}] * 1,21");

        await PonerValorAsync(cliente, idHoras, tarea, "2");

        var resultado = await CampoDeAsync(cliente, tarea, idConIva);

        resultado.GetProperty("valor").GetString().Should().Be("242");
    }

    /// <summary>Cambiar un ingrediente cambia el resultado sin tocar nada más.</summary>
    [Fact]
    public async Task El_resultado_sigue_al_valor_del_que_depende()
    {
        var cliente = await AutenticarAsync();
        var tarea = Guid.NewGuid();

        var horas = Unico("Horas");
        var idHoras = await DefinirAsync(cliente, horas, "Numero");
        var idDoble = await DefinirAsync(cliente, Unico("Doble"), "Formula", $"[{horas}] * 2");

        await PonerValorAsync(cliente, idHoras, tarea, "10");
        (await CampoDeAsync(cliente, tarea, idDoble)).GetProperty("valor").GetString().Should().Be("20");

        await PonerValorAsync(cliente, idHoras, tarea, "21");
        (await CampoDeAsync(cliente, tarea, idDoble)).GetProperty("valor").GetString().Should().Be("42",
            "el valor se calcula al leerlo, así que no puede quedarse desfasado");
    }

    [Fact]
    public async Task Un_campo_calculado_no_se_puede_rellenar_a_mano()
    {
        var cliente = await AutenticarAsync();

        var horas = Unico("Horas");
        await DefinirAsync(cliente, horas, "Numero");
        var idTotal = await DefinirAsync(cliente, Unico("Total"), "Formula", $"[{horas}] * 2");

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/v1/custom-fields/values/{idTotal}/{Guid.NewGuid()}", new { valor = "999" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "aceptarlo y luego ignorarlo al leer sería peor: el valor desaparecería sin explicación");
    }

    #region Lo que se rechaza al definirlo

    [Fact]
    public async Task Una_formula_mal_escrita_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await IntentarDefinirAsync(cliente, Unico("Roto"), "Formula", "2 * (3 + ");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_campo_calculado_sin_formula_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await IntentarDefinirAsync(cliente, Unico("Sin formula"), "Formula", null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Una_formula_que_apunta_a_un_campo_inexistente_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await IntentarDefinirAsync(
            cliente, Unico("Fantasma"), "Formula", "[Campo Que No Existe] + 1");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("Campo Que No Existe");
    }

    /// <summary>
    /// Sumar un campo de texto no tiene sentido, y dejarlo pasar haría que la fórmula funcionara
    /// o no según lo que alguien tecleara ese día en un campo libre.
    /// </summary>
    [Fact]
    public async Task Una_formula_no_puede_usar_un_campo_de_texto()
    {
        var cliente = await AutenticarAsync();

        var nota = Unico("Nota");
        await DefinirAsync(cliente, nota, "Texto");

        var respuesta = await IntentarDefinirAsync(cliente, Unico("Cuenta"), "Formula", $"[{nota}] + 1");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// El ciclo es lo que de verdad importa detener: sin esta comprobación, abrir una tarea con
    /// esas dos fórmulas agota la pila y tira la petición.
    /// </summary>
    [Fact]
    public async Task Una_formula_que_cierra_un_ciclo_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var a = Unico("A");
        var b = Unico("B");

        var horas = Unico("Horas");
        await DefinirAsync(cliente, horas, "Numero");

        // A se apoya en algo neutro; luego B se apoya en A; y entonces se intenta que A se
        // apoye en B, lo que cerraría A → B → A.
        var idA = await DefinirAsync(cliente, a, "Formula", $"[{horas}] + 1");
        await DefinirAsync(cliente, b, "Formula", $"[{a}] * 2");

        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/custom-fields/{idA}", new
        {
            nombre = a,
            obligatorio = false,
            opciones = (string[]?)null,
            posicion = 0,
            formula = $"[{b}] + 1",
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("sí misma");
    }

    [Fact]
    public async Task Una_formula_que_se_referencia_a_si_misma_se_rechaza()
    {
        var cliente = await AutenticarAsync();
        var nombre = Unico("Recursivo");

        var respuesta = await IntentarDefinirAsync(cliente, nombre, "Formula", $"[{nombre}] + 1");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}
