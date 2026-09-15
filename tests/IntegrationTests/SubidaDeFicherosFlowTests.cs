using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Subir un fichero a un documento.
///
/// <b>Esto nunca había funcionado.</b> El endpoint, su handler y el servicio de almacenamiento
/// estaban escritos desde el principio, y el único almacenamiento que había subía a Cloudinary con
/// unas credenciales que <b>no están configuradas en ninguna parte</b>: ni en desarrollo, ni en
/// producción, ni en el compose. La respuesta era un 400 con «Cloud name must be specified in
/// Account!». Tampoco lo llamaba nadie desde la pantalla, así que no se había notado.
///
/// La prueba comprueba las dos mitades: que la subida devuelve una dirección, y que <b>por esa
/// dirección se puede recuperar el fichero</b>. Comprobar sólo la primera dejaría pasar un
/// almacenamiento que guarda donde nadie puede leer.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SubidaDeFicherosFlowTests(CrmApiFactory factory)
{
    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static async Task<HttpResponseMessage> SubirAsync(
        HttpClient cliente, string nombre, string contenido, string tipo)
    {
        using var formulario = new MultipartFormDataContent();
        var fichero = new ByteArrayContent(Encoding.UTF8.GetBytes(contenido));
        fichero.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        formulario.Add(fichero, "file", nombre);

        return await cliente.PostAsync("/api/v1/docs/upload", formulario);
    }

    /// <summary>Lo que se sube se puede volver a leer por la dirección que devuelve.</summary>
    [Fact]
    public async Task Un_fichero_subido_se_puede_recuperar()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await SubirAsync(cliente, "apuntes.txt", "hola mundo", "text/plain");
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var url = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString();
        url.Should().NotBeNullOrWhiteSpace();

        // Una dirección absoluta significa que el almacenamiento es Cloudinary y el fichero vive
        // fuera; entonces no se puede comprobar desde aquí sin salir a la red, y una prueba que
        // dependa de un servicio externo falla por motivos que no son el código.
        if (url!.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;

        var descarga = await cliente.GetAsync(url);
        descarga.StatusCode.Should().Be(HttpStatusCode.OK,
            "de nada sirve guardar el fichero si no se puede leer por la dirección que se devuelve");

        (await descarga.Content.ReadAsStringAsync()).Should().Be("hola mundo");
    }

    /// <summary>
    /// Dos ficheros con el mismo nombre no se pisan.
    ///
    /// Sin esto, la segunda captura llamada «imagen.png» sustituiría a la primera y el documento
    /// de otra persona cambiaría de imagen sin que nadie lo tocara.
    /// </summary>
    [Fact]
    public async Task Dos_ficheros_con_el_mismo_nombre_no_se_pisan()
    {
        var cliente = await AutenticarAsync();

        var primera = await SubirAsync(cliente, "captura.txt", "la primera", "text/plain");
        var segunda = await SubirAsync(cliente, "captura.txt", "la segunda", "text/plain");

        var urlPrimera = (await primera.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        var urlSegunda = (await segunda.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;

        urlSegunda.Should().NotBe(urlPrimera);

        if (urlPrimera.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;

        var contenidoPrimera = await (await cliente.GetAsync(urlPrimera)).Content.ReadAsStringAsync();
        contenidoPrimera.Should().Be("la primera", "la segunda subida no puede sobrescribir a la primera");
    }

    /// <summary>Un fichero vacío se rechaza en vez de guardarse como un cero bytes cualquiera.</summary>
    [Fact]
    public async Task Un_fichero_vacio_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await SubirAsync(cliente, "vacio.txt", string.Empty, "text/plain");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
