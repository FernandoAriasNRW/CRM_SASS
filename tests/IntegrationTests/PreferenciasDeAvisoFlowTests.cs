using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Las preferencias de aviso, de punta a punta.
///
/// Existen por un defecto que la pantalla escondía perfectamente: `GET /preferences` devolvía un
/// objeto con valores fijos escritos en el propio endpoint y `PUT` respondía con el mismo cuerpo
/// que recibía, **sin guardar nada**. Los interruptores se movían, salía el aviso de
/// «guardado»… y al recargar todo volvía a su sitio.
///
/// La prueba que lo destapa no puede quedarse en el código de estado ni en lo que devuelve el
/// `PUT`: hay que **volver a preguntar** en otra petición. Es la misma lección del `PATCH` que
/// no guardaba, en la Fase 4: una prueba que sólo mira el código de estado no ve un guardado
/// que no guarda.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PreferenciasDeAvisoFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";
    private const string Ruta = "/api/v1/notifications/preferences";

    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    /// <summary>Un cuerpo completo, para no depender de los valores por omisión al enviar.</summary>
    private static object Cuerpo(
        bool emailEnabled = true, bool taskCompleted = false, bool exportReady = true,
        bool quietHoursEnabled = false, string inicio = "22:00", string fin = "08:00") => new
        {
            emailEnabled,
            pushEnabled = false,
            taskAssigned = true,
            taskCompleted,
            taskDueSoon = true,
            ticketCreated = true,
            ticketUpdated = false,
            projectUpdated = true,
            mentionEnabled = true,
            exportReady,
            quietHoursEnabled,
            quietHoursStart = inicio,
            quietHoursEnd = fin,
        };

    [Fact]
    public async Task Quien_no_las_ha_tocado_recibe_las_de_por_defecto()
    {
        var cliente = await AutenticarAsync();

        var preferencias = await cliente.GetFromJsonAsync<JsonElement>(Ruta);

        preferencias.GetProperty("taskAssigned").GetBoolean().Should().BeTrue("un aviso que la persona espera llega encendido");
        preferencias.GetProperty("exportReady").GetBoolean().Should().BeTrue("era una condición del encargo");
        preferencias.GetProperty("quietHoursStart").GetString().Should().Be("22:00");
    }

    /// <summary>
    /// La prueba que importa: se guarda, se vuelve a preguntar **en otra petición**, y tiene
    /// que salir lo guardado. Con el endpoint anterior esto fallaba.
    /// </summary>
    [Fact]
    public async Task Lo_que_se_guarda_sigue_ahi_al_volver_a_preguntar()
    {
        var cliente = await AutenticarAsync();

        var guardado = await cliente.PutAsJsonAsync(Ruta, Cuerpo(emailEnabled: false, taskCompleted: true));
        guardado.StatusCode.Should().Be(HttpStatusCode.OK);

        var releidas = await cliente.GetFromJsonAsync<JsonElement>(Ruta);

        releidas.GetProperty("emailEnabled").GetBoolean().Should().BeFalse();
        releidas.GetProperty("taskCompleted").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// El aviso de exportación se puede apagar. Es lo que se pidió explícitamente: llega
    /// encendido, pero la persona manda.
    /// </summary>
    [Fact]
    public async Task El_aviso_de_exportacion_se_puede_apagar_y_se_queda_apagado()
    {
        var cliente = await AutenticarAsync();

        await cliente.PutAsJsonAsync(Ruta, Cuerpo(exportReady: false));

        var releidas = await cliente.GetFromJsonAsync<JsonElement>(Ruta);
        releidas.GetProperty("exportReady").GetBoolean().Should().BeFalse();

        // Y se puede volver a encender: una preferencia que sólo se puede apagar es una trampa.
        await cliente.PutAsJsonAsync(Ruta, Cuerpo(exportReady: true));
        (await cliente.GetFromJsonAsync<JsonElement>(Ruta)).GetProperty("exportReady").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Las_horas_de_silencio_se_guardan()
    {
        var cliente = await AutenticarAsync();

        await cliente.PutAsJsonAsync(Ruta, Cuerpo(quietHoursEnabled: true, inicio: "23:30", fin: "07:15"));

        var releidas = await cliente.GetFromJsonAsync<JsonElement>(Ruta);

        releidas.GetProperty("quietHoursEnabled").GetBoolean().Should().BeTrue();
        releidas.GetProperty("quietHoursStart").GetString().Should().Be("23:30");
        releidas.GetProperty("quietHoursEnd").GetString().Should().Be("07:15");
    }

    /// <summary>
    /// Una hora ilegible se rechaza en vez de sustituirse por algo razonable. Guardar en
    /// silencio unas horas distintas de las que la persona escribió es de los fallos que se
    /// descubren semanas después, al no recibir un aviso.
    /// </summary>
    [Fact]
    public async Task Una_hora_que_no_se_entiende_da_error_en_vez_de_inventarse()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PutAsJsonAsync(Ruta, Cuerpo(quietHoursEnabled: true, inicio: "las diez"));

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Un tramo de silencio de longitud cero no silencia nada: sólo puede ser un error.</summary>
    [Fact]
    public async Task Un_silencio_que_empieza_y_acaba_a_la_misma_hora_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            Ruta, Cuerpo(quietHoursEnabled: true, inicio: "22:00", fin: "22:00"));

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sin_autenticar_no_se_ven_ni_se_cambian()
    {
        var anonimo = factory.CreateClient();

        (await anonimo.GetAsync(Ruta)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonimo.PutAsJsonAsync(Ruta, Cuerpo())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
