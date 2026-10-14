using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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
public sealed class FileUploadFlowTests(CrmApiFactory factory)
{
    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, string name, string content, string type)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(file, "file", name);

        return await client.PostAsync("/api/v1/docs/upload", form);
    }

    /// <summary>Lo que se sube se puede volver a leer por la dirección que devuelve.</summary>
    [Fact]
    public async Task An_uploaded_file_can_be_retrieved()
    {
        var client = await AuthenticateAsync();

        var response = await UploadAsync(client, "apuntes.txt", "hola mundo", "text/plain");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var url = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString();
        url.Should().NotBeNullOrWhiteSpace();

        // Una dirección absoluta significa que el almacenamiento es Cloudinary y el fichero vive
        // fuera; entonces no se puede comprobar desde aquí sin salir a la red, y una prueba que
        // dependa de un servicio externo falla por motivos que no son el código.
        if (url!.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;

        var download = await client.GetAsync(url);
        download.StatusCode.Should().Be(HttpStatusCode.OK,
            "de nada sirve guardar el fichero si no se puede leer por la dirección que se devuelve");

        (await download.Content.ReadAsStringAsync()).Should().Be("hola mundo");
    }

    /// <summary>
    /// Dos ficheros con el mismo nombre no se pisan.
    ///
    /// Sin esto, la segunda captura llamada «imagen.png» sustituiría a la primera y el documento
    /// de otra persona cambiaría de imagen sin que nadie lo tocara.
    /// </summary>
    [Fact]
    public async Task Two_files_with_the_same_name_do_not_overwrite()
    {
        var client = await AuthenticateAsync();

        var first = await UploadAsync(client, "captura.txt", "la primera", "text/plain");
        var second = await UploadAsync(client, "captura.txt", "la segunda", "text/plain");

        var firstUrl = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        var secondUrl = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;

        secondUrl.Should().NotBe(firstUrl);

        if (firstUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;

        var firstContent = await (await client.GetAsync(firstUrl)).Content.ReadAsStringAsync();
        firstContent.Should().Be("la primera", "la segunda subida no puede sobrescribir a la primera");
    }

    /// <summary>Un fichero vacío se rechaza en vez de guardarse como un cero bytes cualquiera.</summary>
    [Fact]
    public async Task An_empty_file_is_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await UploadAsync(client, "vacio.txt", string.Empty, "text/plain");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Una página web no se puede subir.
    ///
    /// Los ficheros se sirven desde el origen de la API, donde vive la cookie de sesión: un .html
    /// subido y abierto ahí podía pedir un token nuevo con la cookie de quien lo abriera.
    /// </summary>
    [Theory]
    [InlineData("pagina.html", "text/html")]
    [InlineData("dibujo.svg", "image/svg+xml")]
    [InlineData("codigo.js", "text/javascript")]
    public async Task A_file_the_browser_would_run_is_rejected(string name, string type)
    {
        var client = await AuthenticateAsync();

        var response = await UploadAsync(client, name, "<script>fetch('/api/v1/auth/refresh')</script>", type);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Lo que se sirve va con un sandbox y sin adivinar el tipo, también lo que ya estuviera
    /// subido de antes: un .html antiguo se descarga en vez de abrirse como página.
    /// </summary>
    [Fact]
    public async Task Stored_files_are_served_sandboxed_and_old_pages_as_downloads()
    {
        var client = await AuthenticateAsync();

        var response = await UploadAsync(client, "notas.txt", "hola", "text/plain");
        var url = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;

        var download = await client.GetAsync(url);
        download.Headers.GetValues("Content-Security-Policy").Single().Should().StartWith("sandbox");
        download.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");

        // Una página que ya estaba en el almacenamiento, de antes de rechazarlas al subir.
        var folder = BuildingBlocks.Infrastructure.Storage.DiskStorageService.ResolveFolder(
            factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()[$"{BuildingBlocks.Infrastructure.Storage.DiskStorageOptions.SectionName}:Folder"]);
        var name = $"antigua-{Guid.NewGuid():N}.html";
        await File.WriteAllTextAsync(Path.Combine(folder, name), "<script>alert(1)</script>");

        var old = await client.GetAsync(url[..(url.LastIndexOf('/') + 1)] + name);
        old.StatusCode.Should().Be(HttpStatusCode.OK);
        old.Content.Headers.ContentDisposition?.DispositionType.Should().Be("attachment");
        old.Headers.GetValues("Content-Security-Policy").Single().Should().StartWith("sandbox");
    }
}
