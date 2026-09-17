using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que archivar y borrar hagan lo que dicen, y —sobre todo— que lo escondido siga escondido.
///
/// El plan de la Fase 5 avisaba de que el archivado «es la parte que se rompe en silencio: una
/// consulta que se olvide de filtrar enseña archivado como si estuviera vivo». Por eso la
/// comprobación que importa no es «el endpoint responde 204», sino **«desapareció de la lista y
/// aparece en la del archivo»**.
///
/// Todo lo que se archiva o se borra aquí se deja como estaba al terminar. Estas pruebas
/// comparten inquilino con las demás de la colección, y una tarea que se quedara en la papelera
/// cambiaría las cuentas de otra prueba sin que su fallo apuntara aquí. Ya pasó una vez, con
/// unas reglas de automatización sin condiciones que cambiaban la prioridad de todo lo que otras
/// pruebas creaban: pasaban solas y fallaban juntas.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ArchivoYPapeleraFlowTests(CrmApiFactory factory)
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

    private static async Task<(int Total, Guid[] Ids)> ListarAsync(HttpClient cliente, string ruta)
    {
        var respuesta = await cliente.GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, ruta);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var ids = cuerpo.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();

        return (cuerpo.GetProperty("totalCount").GetInt32(), ids);
    }

    /// <summary>Un ticket cualquiera del sembrador, para trastear con él y devolverlo a su sitio.</summary>
    private static async Task<Guid> UnTicketAsync(HttpClient cliente)
    {
        var (_, ids) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=1");
        ids.Should().NotBeEmpty("el sembrador crea tickets; sin ellos esto no comprueba nada");
        return ids[0];
    }

    #region Archivado

    /// <summary>
    /// La prueba que justifica que el filtro sea global y no un <c>Where</c> en cada consulta.
    ///
    /// Si el archivado se hubiera implementado consulta a consulta, ésta pasaría en el listado
    /// —donde alguien se acordó— y fallaría en cualquier otro sitio donde se olvidara.
    /// </summary>
    [Fact]
    public async Task Un_ticket_archivado_desaparece_de_la_lista_normal()
    {
        var cliente = await AutenticarAsync();
        var ticketId = await UnTicketAsync(cliente);

        try
        {
            var (antes, _) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=1");

            (await cliente.PostAsync($"/api/v1/tickets/{ticketId}/archive", null))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (despues, _) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=1");

            despues.Should().Be(antes - 1, "archivar saca el ticket de la lista de siempre");

            var (_, archivados) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=100&filter=archived");
            archivados.Should().Contain(ticketId, "y lo pone en el archivo, que es donde se va a buscar");
        }
        finally
        {
            await cliente.PostAsync($"/api/v1/tickets/{ticketId}/unarchive", null);
        }
    }

    [Fact]
    public async Task Desarchivar_lo_devuelve_a_la_lista()
    {
        var cliente = await AutenticarAsync();
        var ticketId = await UnTicketAsync(cliente);

        await cliente.PostAsync($"/api/v1/tickets/{ticketId}/archive", null);
        await cliente.PostAsync($"/api/v1/tickets/{ticketId}/unarchive", null);

        var (_, ids) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=200");
        ids.Should().Contain(ticketId);
    }

    /// <summary>
    /// El archivo enseña **sólo** lo archivado. Si enseñara todo, sería otra entrada de menú que
    /// promete y devuelve la lista de siempre, que es el fallo que toda la Fase 5A viene a
    /// arreglar.
    /// </summary>
    [Fact]
    public async Task El_archivo_no_es_la_lista_entera()
    {
        var cliente = await AutenticarAsync();

        var (todos, _) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=1");
        var (archivados, _) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=1&filter=archived");

        todos.Should().BeGreaterThan(0);
        archivados.Should().BeLessThan(todos);
    }

    /// <summary>Archivar dos veces no es un error: es el estado que se pedía.</summary>
    [Fact]
    public async Task Archivar_dos_veces_deja_el_mismo_estado()
    {
        var cliente = await AutenticarAsync();
        var ticketId = await UnTicketAsync(cliente);

        try
        {
            await cliente.PostAsync($"/api/v1/tickets/{ticketId}/archive", null);

            (await cliente.PostAsync($"/api/v1/tickets/{ticketId}/archive", null))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (cuantos, _) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=200&filter=archived");
            cuantos.Should().BeGreaterThan(0);
        }
        finally
        {
            await cliente.PostAsync($"/api/v1/tickets/{ticketId}/unarchive", null);
        }
    }

    #endregion

    #region Papelera

    /// <summary>
    /// El borrado que no borraba.
    ///
    /// <c>DELETE /tickets/{id}</c> leía el ticket, comprobaba que existía y devolvía 204 **sin
    /// tocar nada**: la pantalla decía «borrado» y el ticket seguía ahí al recargar. El de tareas
    /// hacía lo mismo por otro camino —cargaba la tarea y la volvía a guardar sin cambios—.
    /// </summary>
    [Fact]
    public async Task Borrar_un_ticket_lo_saca_de_la_lista_de_verdad()
    {
        var cliente = await AutenticarAsync();
        var ticketId = await UnTicketAsync(cliente);

        try
        {
            (await cliente.DeleteAsync($"/api/v1/tickets/{ticketId}"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (_, visibles) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=200");
            visibles.Should().NotContain(ticketId, "antes esto devolvía 204 y no borraba nada");

            var (_, papelera) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=200&filter=trash");
            papelera.Should().Contain(ticketId, "borrar es mandar a la papelera, no evaporar");
        }
        finally
        {
            await cliente.PostAsync($"/api/v1/tickets/{ticketId}/restore", null);
        }
    }

    [Fact]
    public async Task Restaurar_lo_devuelve_a_la_lista()
    {
        var cliente = await AutenticarAsync();
        var ticketId = await UnTicketAsync(cliente);

        await cliente.DeleteAsync($"/api/v1/tickets/{ticketId}");

        (await cliente.PostAsync($"/api/v1/tickets/{ticketId}/restore", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (_, ids) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=200");
        ids.Should().Contain(ticketId);
    }

    /// <summary>
    /// Archivar y borrar no se pisan: lo que estaba archivado sigue archivado al salir de la
    /// papelera. Con una sola columna para las dos cosas, restaurar lo habría devuelto a la lista
    /// principal, que no es donde su dueño lo dejó.
    /// </summary>
    [Fact]
    public async Task Restaurar_algo_archivado_lo_deja_archivado()
    {
        var cliente = await AutenticarAsync();
        var ticketId = await UnTicketAsync(cliente);

        try
        {
            await cliente.PostAsync($"/api/v1/tickets/{ticketId}/archive", null);
            await cliente.DeleteAsync($"/api/v1/tickets/{ticketId}");
            await cliente.PostAsync($"/api/v1/tickets/{ticketId}/restore", null);

            var (_, visibles) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=200");
            visibles.Should().NotContain(ticketId, "sigue archivado, así que no vuelve a la lista normal");

            var (_, archivados) = await ListarAsync(cliente, "/api/v1/tickets?pageSize=200&filter=archived");
            archivados.Should().Contain(ticketId);
        }
        finally
        {
            await cliente.PostAsync($"/api/v1/tickets/{ticketId}/unarchive", null);
        }
    }

    /// <summary>
    /// Una tarea borrada tampoco vuelve por la puerta de atrás.
    /// </summary>
    [Fact]
    public async Task Una_tarea_borrada_sale_de_la_lista_y_vuelve_al_restaurarla()
    {
        var cliente = await AutenticarAsync();

        var (_, ids) = await ListarAsync(cliente, "/api/v1/tasks?pageSize=1");
        ids.Should().NotBeEmpty("el sembrador crea tareas");
        var tareaId = ids[0];

        try
        {
            (await cliente.DeleteAsync($"/api/v1/tasks/{tareaId}"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (_, visibles) = await ListarAsync(cliente, "/api/v1/tasks?pageSize=200");
            visibles.Should().NotContain(tareaId,
                "el handler cargaba la tarea, la volvía a guardar sin tocarla y devolvía éxito");

            var (_, papelera) = await ListarAsync(cliente, "/api/v1/tasks?pageSize=200&filter=trash");
            papelera.Should().Contain(tareaId);
        }
        finally
        {
            await cliente.PostAsync($"/api/v1/tasks/{tareaId}/restore", null);
        }
    }

    #endregion

    #region El menú entero

    /// <summary>
    /// Las entradas del menú responden en los tres módulos que lo llevan.
    ///
    /// Es una comprobación de contrato, no de contenido: lo que vigila es que ningún módulo se
    /// quede sin entender un filtro que el panel ofrece. Cuando eso pasa, el módulo no falla:
    /// devuelve la lista entera, que es peor.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/tickets")]
    [InlineData("/api/v1/tasks")]
    [InlineData("/api/v1/projects")]
    public async Task Todas_las_entradas_del_menu_las_entienden_los_tres_modulos(string ruta)
    {
        var cliente = await AutenticarAsync();

        foreach (var filtro in new[] { "mine", "created", "favorites", "shared", "private", "archived", "trash" })
        {
            var respuesta = await cliente.GetAsync($"{ruta}?pageSize=1&filter={filtro}");

            respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
                $"«{filtro}» se ofrece en el panel de navegación, así que {ruta} tiene que entenderlo");
        }
    }

    #endregion
}
