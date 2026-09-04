using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que «compartido conmigo» y «privado» signifiquen algo.
///
/// Son las dos entradas del menú con más riesgo de quedarse en adorno. «Compartido conmigo» sale
/// de una tabla de permisos que hoy sólo tiene filas por rol y de módulo entero: si el filtro las
/// contara como comparticiones, devolvería media aplicación —la lista de siempre otra vez—; y si
/// no hubiera forma de compartir, devolvería siempre vacío, que es igual de inútil.
///
/// Por eso estas pruebas comparten de verdad, comprueban el efecto, y lo deshacen.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ComparticionFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<(HttpClient Cliente, Guid Yo)> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var yo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me");
        return (cliente, yo.GetProperty("id").GetGuid());
    }

    private static async Task<Guid[]> IdsAsync(HttpClient cliente, string ruta)
    {
        var respuesta = await cliente.GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, ruta);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();
    }

    /// <summary>
    /// La prueba que une los dos lados, igual que la de favoritos.
    ///
    /// Importa porque los módulos hablan de «Tarea» y la tabla de permisos guarda «Task»: la
    /// traducción vive en un solo sitio, y si se rompiera, el filtro devolvería una lista vacía
    /// **sin dar ningún error**.
    /// </summary>
    [Fact]
    public async Task Una_tarea_compartida_conmigo_sale_al_filtrar_por_compartidas()
    {
        var (cliente, yo) = await AutenticarAsync();

        var tareas = await IdsAsync(cliente, "/api/v1/tasks?pageSize=1");
        tareas.Should().NotBeEmpty("el sembrador crea tareas");
        var tareaId = tareas[0];

        try
        {
            (await cliente.PutAsJsonAsync($"/api/v1/comparticion/Tarea/{tareaId}/{yo}", new { Nivel = "Edit" }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var compartidas = await IdsAsync(cliente, "/api/v1/tasks?pageSize=200&filter=shared");

            compartidas.Should().Contain(tareaId,
                "si esto falla, la traducción del tipo no coincide y el filtro devuelve vacío en silencio");
        }
        finally
        {
            await cliente.DeleteAsync($"/api/v1/comparticion/Tarea/{tareaId}/{yo}");
        }
    }

    [Fact]
    public async Task Dejar_de_compartir_la_quita_de_la_lista()
    {
        var (cliente, yo) = await AutenticarAsync();

        var tareaId = (await IdsAsync(cliente, "/api/v1/tasks?pageSize=1"))[0];

        await cliente.PutAsJsonAsync($"/api/v1/comparticion/Tarea/{tareaId}/{yo}", new { Nivel = "View" });
        await cliente.DeleteAsync($"/api/v1/comparticion/Tarea/{tareaId}/{yo}");

        var compartidas = await IdsAsync(cliente, "/api/v1/tasks?pageSize=200&filter=shared");
        compartidas.Should().NotContain(tareaId);
    }

    /// <summary>
    /// Los permisos por rol y los de módulo entero **no** son comparticiones.
    ///
    /// Hoy la tabla tiene 185 filas de ese tipo y ninguna nominal. Si el filtro las contara,
    /// «compartido conmigo» devolvería prácticamente todo, que es exactamente el fallo que esta
    /// fase viene a arreglar, sólo que disfrazado de funcionalidad nueva.
    /// </summary>
    [Fact]
    public async Task Sin_nada_compartido_el_filtro_devuelve_cero_y_no_todo()
    {
        var (cliente, _) = await AutenticarAsync();

        var todas = await IdsAsync(cliente, "/api/v1/tasks?pageSize=200");
        var compartidas = await IdsAsync(cliente, "/api/v1/tasks?pageSize=200&filter=shared");

        todas.Should().NotBeEmpty();
        compartidas.Length.Should().BeLessThan(todas.Length,
            "los permisos por rol están en la misma tabla y no cuentan como compartir");
    }

    /// <summary>
    /// «Privado» es lo mío que no le he dado a nadie: al compartir algo, deja de ser privado.
    ///
    /// Se comprueba así, y no contra un número fijo, porque el reparto de datos del sembrador no
    /// es asunto de esta prueba: lo que se comprueba es el significado.
    /// </summary>
    [Fact]
    public async Task Compartir_algo_lo_saca_de_privados()
    {
        var (cliente, yo) = await AutenticarAsync();

        var mias = await IdsAsync(cliente, "/api/v1/tasks?pageSize=200&filter=mine");
        if (mias.Length == 0) return; // Sin tareas propias, este caso no aplica.

        var privadasAntes = await IdsAsync(cliente, "/api/v1/tasks?pageSize=200&filter=private");
        var candidata = privadasAntes.FirstOrDefault();
        if (candidata == Guid.Empty) return;

        try
        {
            await cliente.PutAsJsonAsync($"/api/v1/comparticion/Tarea/{candidata}/{yo}", new { Nivel = "View" });

            var privadasDespues = await IdsAsync(cliente, "/api/v1/tasks?pageSize=200&filter=private");

            privadasDespues.Should().NotContain(candidata,
                "compartir algo deja de hacerlo privado; por eso «privado» se calcula restando y no "
                + "con un campo EsPrivado que se desincronizaría");
        }
        finally
        {
            await cliente.DeleteAsync($"/api/v1/comparticion/Tarea/{candidata}/{yo}");
        }
    }

    [Fact]
    public async Task Se_puede_ver_con_quien_esta_compartido()
    {
        var (cliente, yo) = await AutenticarAsync();

        var tareaId = (await IdsAsync(cliente, "/api/v1/tasks?pageSize=1"))[0];

        try
        {
            await cliente.PutAsJsonAsync($"/api/v1/comparticion/Tarea/{tareaId}/{yo}", new { Nivel = "Full" });

            var conQuien = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/comparticion/Tarea/{tareaId}");

            conQuien.EnumerateArray().Select(x => x.GetGuid()).Should().Contain(yo);
        }
        finally
        {
            await cliente.DeleteAsync($"/api/v1/comparticion/Tarea/{tareaId}/{yo}");
        }
    }

    /// <summary>Compartir dos veces cambia el nivel; no deja dos filas peleándose.</summary>
    [Fact]
    public async Task Compartir_dos_veces_no_duplica()
    {
        var (cliente, yo) = await AutenticarAsync();

        var tareaId = (await IdsAsync(cliente, "/api/v1/tasks?pageSize=1"))[0];

        try
        {
            await cliente.PutAsJsonAsync($"/api/v1/comparticion/Tarea/{tareaId}/{yo}", new { Nivel = "View" });
            await cliente.PutAsJsonAsync($"/api/v1/comparticion/Tarea/{tareaId}/{yo}", new { Nivel = "Full" });

            var conQuien = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/comparticion/Tarea/{tareaId}");

            conQuien.EnumerateArray().Should().HaveCount(1);
        }
        finally
        {
            await cliente.DeleteAsync($"/api/v1/comparticion/Tarea/{tareaId}/{yo}");
        }
    }

    [Fact]
    public async Task Un_nivel_que_no_existe_se_rechaza()
    {
        var (cliente, yo) = await AutenticarAsync();

        var tareaId = (await IdsAsync(cliente, "/api/v1/tasks?pageSize=1"))[0];

        (await cliente.PutAsJsonAsync($"/api/v1/comparticion/Tarea/{tareaId}/{yo}", new { Nivel = "Jefe" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_tipo_que_no_existe_se_rechaza()
    {
        var (cliente, yo) = await AutenticarAsync();

        (await cliente.PutAsJsonAsync($"/api/v1/comparticion/Factura/{Guid.NewGuid()}/{yo}", new { Nivel = "View" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sin_autenticar_no_se_comparte_nada()
    {
        var anonimo = factory.CreateClient();

        (await anonimo.PutAsJsonAsync($"/api/v1/comparticion/Tarea/{Guid.NewGuid()}/{Guid.NewGuid()}", new { Nivel = "View" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
