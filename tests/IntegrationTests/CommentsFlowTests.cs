using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Comentarios, de punta a punta contra la API real.
///
/// Existen porque hasta ahora **no existían**: el panel de detalle de tarea llevaba su interfaz
/// de comentarios escrita y `GET /tasks/{id}/comments` devolvía 404 en cada apertura. No es que
/// fallara: es que no había ninguna entidad de comentario en todo el backend.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CommentsFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var cuerpo = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = cuerpo.GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static async Task<Guid> ComentarAsync(HttpClient cliente, string entidad, Guid entityId, string texto, Guid? respondeA = null)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/v1/comments/{entidad}/{entityId}", new { texto, respondeAId = respondeA });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<JsonElement> HiloAsync(HttpClient cliente, string entidad, Guid entityId) =>
        cliente.GetFromJsonAsync<JsonElement>($"/api/v1/comments/{entidad}/{entityId}");

    [Theory]
    [InlineData("Tarea")]
    [InlineData("Ticket")]
    [InlineData("Proyecto")]
    public async Task Se_comenta_y_se_recupera_en_las_tres_entidades(string entidad)
    {
        var cliente = await AutenticarAsync();
        var entityId = Guid.NewGuid();

        await ComentarAsync(cliente, entidad, entityId, "Primer comentario");

        var hilo = await HiloAsync(cliente, entidad, entityId);
        hilo.EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("texto").GetString().Should().Be("Primer comentario");
    }

    /// <summary>El hilo se lee en orden: del más antiguo al más nuevo.</summary>
    [Fact]
    public async Task El_hilo_sale_en_orden_de_escritura()
    {
        var cliente = await AutenticarAsync();
        var entityId = Guid.NewGuid();

        await ComentarAsync(cliente, "Tarea", entityId, "Primero");
        await ComentarAsync(cliente, "Tarea", entityId, "Segundo");

        var textos = (await HiloAsync(cliente, "Tarea", entityId))
            .EnumerateArray().Select(c => c.GetProperty("texto").GetString()).ToList();

        textos.Should().Equal("Primero", "Segundo");
    }

    /// <summary>Un hilo vacío es una lista vacía, no un 404. Era justo el defecto que se arregla.</summary>
    [Fact]
    public async Task Una_entidad_sin_comentarios_devuelve_una_lista_vacia()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.GetAsync($"/api/v1/comments/Tarea/{Guid.NewGuid()}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task El_autor_edita_su_comentario_y_queda_marcado_como_editado()
    {
        var cliente = await AutenticarAsync();
        var entityId = Guid.NewGuid();
        var id = await ComentarAsync(cliente, "Tarea", entityId, "Con errata");

        var edicion = await cliente.PutAsJsonAsync($"/api/v1/comments/{id}", new { texto = "Sin errata" });
        edicion.StatusCode.Should().Be(HttpStatusCode.OK);

        var comentario = (await HiloAsync(cliente, "Tarea", entityId)).EnumerateArray().Single();
        comentario.GetProperty("texto").GetString().Should().Be("Sin errata");
        comentario.GetProperty("editadoUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Un_comentario_vacio_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/v1/comments/Tarea/{Guid.NewGuid()}", new { texto = "   " });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task No_se_puede_comentar_sobre_algo_que_nadie_pinta()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/v1/comments/Factura/{Guid.NewGuid()}", new { texto = "Hola" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Se_puede_responder_a_un_comentario()
    {
        var cliente = await AutenticarAsync();
        var entityId = Guid.NewGuid();
        var padre = await ComentarAsync(cliente, "Tarea", entityId, "Pregunta");

        await ComentarAsync(cliente, "Tarea", entityId, "Respuesta", padre);

        var hilo = await HiloAsync(cliente, "Tarea", entityId);
        hilo.GetArrayLength().Should().Be(2);

        // El comentario original tiene `respondeAId` nulo, así que hay que mirarlo antes de
        // leerlo como Guid: el hilo trae los dos.
        var respuestas = hilo.EnumerateArray()
            .Select(c => c.GetProperty("respondeAId"))
            .Where(r => r.ValueKind != JsonValueKind.Null)
            .Select(r => r.GetGuid())
            .ToList();

        respuestas.Should().Equal(padre);
    }

    /// <summary>
    /// Un solo nivel, como las subtareas: es lo que evita los hilos que se van a la derecha hasta
    /// no caber, y permite pintarlos con una cuenta en vez de recorriendo un árbol.
    /// </summary>
    [Fact]
    public async Task No_se_puede_responder_a_una_respuesta()
    {
        var cliente = await AutenticarAsync();
        var entityId = Guid.NewGuid();
        var padre = await ComentarAsync(cliente, "Tarea", entityId, "Pregunta");
        var respuesta = await ComentarAsync(cliente, "Tarea", entityId, "Respuesta", padre);

        var tercera = await cliente.PostAsJsonAsync(
            $"/api/v1/comments/Tarea/{entityId}", new { texto = "Otra", respondeAId = respuesta });

        tercera.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Sin esta comprobación, una respuesta podría colgarse de un comentario de otra tarea y
    /// aparecer en un hilo donde nadie la escribió.
    /// </summary>
    [Fact]
    public async Task Una_respuesta_no_puede_saltar_a_otro_hilo()
    {
        var cliente = await AutenticarAsync();
        var padre = await ComentarAsync(cliente, "Tarea", Guid.NewGuid(), "En una tarea");

        var intruso = await cliente.PostAsJsonAsync(
            $"/api/v1/comments/Tarea/{Guid.NewGuid()}", new { texto = "En otra", respondeAId = padre });

        intruso.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_comentario_se_borra()
    {
        var cliente = await AutenticarAsync();
        var entityId = Guid.NewGuid();
        var id = await ComentarAsync(cliente, "Tarea", entityId, "Para borrar");

        var borrado = await cliente.DeleteAsync($"/api/v1/comments/{id}");
        borrado.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await HiloAsync(cliente, "Tarea", entityId)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Editar_un_comentario_que_no_existe_da_404()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/comments/{Guid.NewGuid()}", new { texto = "Hola" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Los comentarios de una entidad no se mezclan con los de otra.</summary>
    [Fact]
    public async Task Cada_entidad_tiene_su_propio_hilo()
    {
        var cliente = await AutenticarAsync();
        var unaTarea = Guid.NewGuid();
        var otraTarea = Guid.NewGuid();

        await ComentarAsync(cliente, "Tarea", unaTarea, "De la primera");
        await ComentarAsync(cliente, "Tarea", otraTarea, "De la segunda");

        (await HiloAsync(cliente, "Tarea", unaTarea)).EnumerateArray().Single()
            .GetProperty("texto").GetString().Should().Be("De la primera");
    }
}
