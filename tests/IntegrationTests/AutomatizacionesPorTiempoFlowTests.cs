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
public sealed class AutomatizacionesPorTiempoFlowTests(CrmApiFactory factory)
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

    /// <summary>Los nombres son únicos por inquilino y la colección comparte uno.</summary>
    private static string Unico(string nombre) => $"{nombre} {Guid.NewGuid().ToString()[..8]}";

    private static Task<HttpResponseMessage> DefinirAsync(
        HttpClient cliente, string nombre, string disparador, object[] condiciones, object[] acciones) =>
        cliente.PostAsJsonAsync("/api/v1/automations", new { nombre, disparador, condiciones, acciones });

    /// <summary>
    /// El vocabulario se sirve, no se repite en el cliente. Si la interfaz ofreciera opciones que
    /// el servidor no conoce, pasaría lo de los informes: media pantalla sin funcionar.
    /// </summary>
    [Fact]
    public async Task El_vocabulario_anuncia_lo_nuevo()
    {
        var cliente = await AutenticarAsync();

        var vocabulario = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/automations/vocabulario");

        static string[] Lista(JsonElement e, string nombre) =>
            e.GetProperty(nombre).EnumerateArray().Select(x => x.GetString()!).ToArray();

        Lista(vocabulario, "disparadores").Should().Contain("TareaPorVencer");
        Lista(vocabulario, "campos").Should().Contain("DiasParaVencer");
        Lista(vocabulario, "operadores").Should().Contain(["MenorOIgual", "MayorOIgual"]);
        Lista(vocabulario, "acciones").Should().Contain("Notificar");

        // Los metadatos que la interfaz necesita para no ofrecer combinaciones que el dominio
        // rechaza. Servidos también, por el mismo motivo.
        Lista(vocabulario, "camposNumericos").Should().Equal("DiasParaVencer");
        Lista(vocabulario, "operadoresNumericos").Should().BeEquivalentTo(["MenorOIgual", "MayorOIgual"]);
        Lista(vocabulario, "disparadoresPorTiempo").Should().Equal("TareaPorVencer");
        vocabulario.GetProperty("destinatarioResponsable").GetString().Should().Be("Responsable");
    }

    /// <summary>La automatización canónica de este tipo de producto, de punta a punta.</summary>
    [Fact]
    public async Task Se_puede_configurar_avisar_al_responsable_dos_dias_antes()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await DefinirAsync(cliente, Unico("Avisar antes de vencer"), "TareaPorVencer",
            condiciones: [new { campo = "DiasParaVencer", operador = "MenorOIgual", valor = "2" }],
            acciones: [new { tipo = "Notificar", valor = "Responsable" }]);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Se_puede_configurar_subir_la_prioridad_de_lo_vencido()
    {
        var cliente = await AutenticarAsync();

        // Días negativos: ya venció. Es la forma de expresar «lleva retraso».
        var respuesta = await DefinirAsync(cliente, Unico("Urgente si lleva retraso"), "TareaPorVencer",
            condiciones: [new { campo = "DiasParaVencer", operador = "MenorOIgual", valor = "-1" }],
            acciones: [new { tipo = "CambiarPrioridad", valor = "Urgent" }]);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Comparar «Estado» con «menor o igual» no tiene sentido y se rechaza al guardar, en vez de
    /// caer en la comparación alfabética que daría una regla que salta cuando no debe.
    /// </summary>
    [Fact]
    public async Task Un_operador_numerico_sobre_un_campo_de_texto_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await DefinirAsync(cliente, Unico("Sin sentido"), "TareaCambiaDeEstado",
            condiciones: [new { campo = "Estado", operador = "MenorOIgual", valor = "Done" }],
            acciones: [new { tipo = "CambiarPrioridad", valor = "High" }]);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_campo_numerico_comparado_con_texto_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await DefinirAsync(cliente, Unico("Pronto"), "TareaPorVencer",
            condiciones: [new { campo = "DiasParaVencer", operador = "Igual", valor = "pronto" }],
            acciones: [new { tipo = "CambiarPrioridad", valor = "High" }]);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #region El registro de ejecuciones

    /// <summary>
    /// Una regla recién creada no tiene historial, y eso es una lista vacía, no un 404: quien
    /// abre el detalle necesita distinguir «no ha pasado nada» de «esto no existe».
    /// </summary>
    [Fact]
    public async Task Una_regla_nueva_tiene_historial_vacio()
    {
        var cliente = await AutenticarAsync();

        // Acotada a un proyecto inexistente. Ver el comentario de la clase: una regla activa sin
        // condiciones en el inquilino compartido cambiaría las tareas de las demás pruebas.
        var alta = await DefinirAsync(cliente, Unico("Sin estrenar"), "TareaCreada",
            condiciones: [new { campo = "ProyectoId", operador = "Igual", valor = Guid.NewGuid().ToString() }],
            acciones: [new { tipo = "CambiarPrioridad", valor = "High" }]);
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var respuesta = await cliente.GetAsync($"/api/v1/automations/{id}/ejecuciones");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Should().BeEmpty();
    }

    /// <summary>
    /// El caso que el contador de la regla no sabía distinguir: la regla salta, la condición no
    /// se cumple y no pasa nada. Sin este registro, quien la configuró ve el contador a cero y
    /// concluye que el disparador está roto, cuando lo que falla es su condición.
    /// </summary>
    [Fact]
    public async Task Se_anota_cuando_la_regla_salta_y_la_condicion_no_se_cumple()
    {
        var cliente = await AutenticarAsync();

        // Condición imposible: ningún proyecto tiene ese identificador.
        var alta = await DefinirAsync(cliente, Unico("Nunca se cumple"), "TareaCreada",
            condiciones: [new { campo = "ProyectoId", operador = "Igual", valor = Guid.NewGuid().ToString() }],
            acciones: [new { tipo = "CambiarPrioridad", valor = "High" }]);
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Crear una tarea dispara TareaCreada.
        var tarea = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = Guid.NewGuid(),
            title = "Tarea que dispara la regla",
            description = "Para ver qué anota el registro",
            assigneeId = Guid.Empty,
            estimatedHours = 1,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
        });
        tarea.EnsureSuccessStatusCode();

        var historial = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/automations/{id}/ejecuciones");

        historial.EnumerateArray().Should().NotBeEmpty("la regla saltó, aunque no hiciera nada");
        historial.EnumerateArray().First().GetProperty("resultado").GetString()
            .Should().Be("NoCumplioCondiciones",
                "es exactamente la información que el contador de la regla no podía dar");
    }

    /// <summary>Y cuando sí se aplica, se anota como aplicada.</summary>
    [Fact]
    public async Task Se_anota_cuando_la_regla_se_aplica()
    {
        var cliente = await AutenticarAsync();

        // La regla se acota a un proyecto que sólo usa esta prueba. Ver el comentario de la
        // clase: una regla activa sin condiciones alcanzaría a las tareas de las demás.
        var miProyecto = Guid.NewGuid();

        var alta = await DefinirAsync(cliente, Unico("Urgente en mi proyecto"), "TareaCreada",
            condiciones: [new { campo = "ProyectoId", operador = "Igual", valor = miProyecto.ToString() }],
            acciones: [new { tipo = "CambiarPrioridad", valor = "Urgent" }]);
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var tarea = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = miProyecto,
            title = "Tarea que se vuelve urgente",
            description = "La regla se aplica porque el proyecto coincide",
            assigneeId = Guid.Empty,
            estimatedHours = 1,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
        });
        var tareaId = (await tarea.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var historial = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/automations/{id}/ejecuciones");

        var mia = historial.EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("entityId").GetGuid() == tareaId);

        mia.ValueKind.Should().NotBe(JsonValueKind.Undefined, "la ejecución sobre esa tarea tiene que constar");
        mia.GetProperty("resultado").GetString().Should().Be("Aplicada");

        // Y la acción ocurrió de verdad, no sólo se anotó.
        var recargada = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tareaId}");
        var prioridad = recargada.GetProperty("priority");
        var valor = prioridad.ValueKind == JsonValueKind.Object
            ? prioridad.GetProperty("value").GetString()
            : prioridad.GetString();

        valor.Should().Be("Urgent", "un registro que dice «aplicada» sin haber cambiado nada sería la peor mentira de todas");
    }

    #endregion
}
