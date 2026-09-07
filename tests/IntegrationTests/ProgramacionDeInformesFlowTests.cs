using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Los informes programados, contra la API levantada.
///
/// Lo que <b>no</b> se comprueba aquí es que un informe salga solo el lunes a las 8: eso exigiría
/// esperar a un lunes. La decisión de cuándo toca es una función pura del dominio
/// —<c>ProgramacionDeInforme.TocaAhora</c>— y está probada al detalle en las unitarias, con sus
/// casos límite: el domingo, el día 1, el minuto antes de la hora, y el que impide que un informe
/// diario llegue doce veces.
///
/// Aquí se comprueba el contrato: que se pueda programar, ver, apagar y quitar, y que lo que no
/// tiene sentido se rechace diciendo por qué.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ProgramacionDeInformesFlowTests(CrmApiFactory factory)
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

    private static async Task<Guid> CrearInformeAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports", new
        {
            Name = $"Programado {Guid.NewGuid():N}"[..30],
            Type = "KpiSummary",
            Format = "Pdf"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Se_puede_programar_un_informe_y_se_lee_de_vuelta()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/reports/{informeId}/programaciones", new
        {
            Frecuencia = "Semanal",
            Formato = "Pdf",
            Hora = "08:00",
            Dia = 1
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());

        // Se relee en otra petición, no se mira lo que devolvió el POST: es la lección del PATCH
        // que respondía 200 sin guardar.
        var lista = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{informeId}/programaciones");

        var suya = lista.EnumerateArray().Should().ContainSingle().Subject;
        suya.GetProperty("frecuencia").GetString().Should().Be("Semanal");
        suya.GetProperty("hora").GetString().Should().Be("08:00");
        suya.GetProperty("dia").GetInt32().Should().Be(1);
        suya.GetProperty("activa").GetBoolean().Should().BeTrue();
        suya.GetProperty("ultimoDiaGenerado").ValueKind.Should().Be(JsonValueKind.Null,
            "recién creada no se ha generado nunca");
    }

    [Fact]
    public async Task Un_informe_recien_creado_no_tiene_programaciones()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var lista = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{informeId}/programaciones");

        lista.EnumerateArray().Should().BeEmpty();
    }

    /// <summary>
    /// Apagar y encender, sin borrar.
    ///
    /// Es la diferencia que importa: apagar conserva la marca del último día generado, así que
    /// volver a encender el mismo día no dispara el informe por segunda vez. Borrar y recrear sí
    /// lo haría.
    /// </summary>
    [Fact]
    public async Task Una_programacion_se_puede_apagar_y_volver_a_encender()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var creada = await cliente.PostAsJsonAsync($"/api/v1/reports/{informeId}/programaciones",
            new { Frecuencia = "Diaria", Formato = "Csv", Hora = "07:30", Dia = (int?)null });

        var id = (await creada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await cliente.PatchAsJsonAsync($"/api/v1/programaciones/{id}", new { Activa = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var apagada = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{informeId}/programaciones");
        apagada.EnumerateArray().Single().GetProperty("activa").GetBoolean().Should().BeFalse();

        (await cliente.PatchAsJsonAsync($"/api/v1/programaciones/{id}", new { Activa = true }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var encendida = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{informeId}/programaciones");
        encendida.EnumerateArray().Single().GetProperty("activa").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Una_programacion_se_puede_quitar()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var creada = await cliente.PostAsJsonAsync($"/api/v1/reports/{informeId}/programaciones",
            new { Frecuencia = "Mensual", Formato = "Excel", Hora = "09:00", Dia = 1 });

        var id = (await creada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await cliente.DeleteAsync($"/api/v1/programaciones/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var lista = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{informeId}/programaciones");
        lista.EnumerateArray().Should().BeEmpty();
    }

    /// <summary>Quitar algo que ya no está no es un error: es el estado que se pedía.</summary>
    [Fact]
    public async Task Quitar_una_que_no_existe_no_es_un_error()
    {
        var cliente = await AutenticarAsync();

        (await cliente.DeleteAsync($"/api/v1/programaciones/{Guid.NewGuid()}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    #region Lo que se rechaza, y por qué

    [Fact]
    public async Task Una_frecuencia_que_no_existe_dice_cuales_hay()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/reports/{informeId}/programaciones",
            new { Frecuencia = "CadaHora", Formato = "Pdf", Hora = "08:00", Dia = (int?)null });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var mensaje = await respuesta.Content.ReadAsStringAsync();
        mensaje.Should().Contain("CadaHora").And.Contain("Diaria");
    }

    [Fact]
    public async Task Una_semanal_sin_dia_se_rechaza()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/reports/{informeId}/programaciones",
            new { Frecuencia = "Semanal", Formato = "Pdf", Hora = "08:00", Dia = (int?)null });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("qué día");
    }

    /// <summary>
    /// El día 31 se rechaza, y el mensaje explica el porqué.
    ///
    /// Un informe mensual el día 31 no se generaría en febrero ni en los meses de treinta días:
    /// cuatro meses al año fallando en silencio. Es mejor no admitirlo que admitirlo y que falle.
    /// </summary>
    [Fact]
    public async Task El_dia_31_se_rechaza_explicando_el_motivo()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/reports/{informeId}/programaciones",
            new { Frecuencia = "Mensual", Formato = "Pdf", Hora = "08:00", Dia = 31 });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("febrero");
    }

    [Fact]
    public async Task Una_hora_que_no_es_una_hora_se_rechaza()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/reports/{informeId}/programaciones",
            new { Frecuencia = "Diaria", Formato = "Pdf", Hora = "por la mañana", Dia = (int?)null });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("08:00", "el mensaje enseña el formato");
    }

    [Fact]
    public async Task Programar_un_informe_que_no_existe_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/reports/{Guid.NewGuid()}/programaciones",
            new { Frecuencia = "Diaria", Formato = "Pdf", Hora = "08:00", Dia = (int?)null });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sin_autenticar_no_se_programa_nada()
    {
        var anonimo = factory.CreateClient();

        (await anonimo.PostAsJsonAsync($"/api/v1/reports/{Guid.NewGuid()}/programaciones",
                new { Frecuencia = "Diaria", Formato = "Pdf", Hora = "08:00", Dia = (int?)null }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
