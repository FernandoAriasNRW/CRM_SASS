using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que <c>search</c> busque en todo el inquilino, y no en la página que se tenga a mano.
///
/// Nace de un fallo del buscador de menciones: pedía las primeras cincuenta filas y filtraba en el
/// navegador. Con datos de prueba funcionaba; con miles de tareas, lo que se busca simplemente no
/// está entre esas cincuenta y el desplegable sale vacío para algo que sí existe. Es un fallo que
/// sólo aparece cuando el cliente crece, que es cuando ya nadie lo relaciona con esto.
///
/// Por eso la comprobación central no es «devuelve 200» ni «devuelve menos»: es
/// <b>«encuentra algo que no está en la primera página»</b>. Eso es exactamente lo que la
/// búsqueda del cliente no podía hacer, y lo único que distingue una búsqueda de verdad de un
/// filtro sobre lo ya descargado.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class BusquedaPorTextoFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    /// <summary>Las listas paginadas que buscan, con el campo por el que se titula cada una.</summary>
    public static TheoryData<string, string> Listas => new()
    {
        { "/api/v1/tasks", "title" },
        { "/api/v1/tickets", "title" },
        { "/api/v1/projects", "name" }
    };

    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static async Task<JsonElement[]> ItemsAsync(HttpClient cliente, string ruta)
    {
        var respuesta = await cliente.GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, ruta);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

        // Las listas paginadas envuelven en `items`; la de personas devuelve el array pelado.
        return cuerpo.ValueKind == JsonValueKind.Array
            ? [.. cuerpo.EnumerateArray()]
            : [.. cuerpo.GetProperty("items").EnumerateArray()];
    }

    /// <summary>
    /// Quita acentos y mayúsculas, sólo para comparar <b>en la prueba</b>.
    ///
    /// No es lo que hace el servidor: allí lo resuelve la colación de la base de datos
    /// (<c>utf8mb4_0900_ai_ci</c>), y precisamente por eso se compara así. Con un <c>Contains</c>
    /// a secas, «Diseño» encontrado por «diseno» parecería un resultado que sobra.
    /// </summary>
    private static string Plano(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sinTildes = new StringBuilder(descompuesto.Length);

        foreach (var letra in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)
                sinTildes.Append(letra);
        }

        return sinTildes.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    private static readonly char[] Separadores = [' ', '-', ':', ',', '.'];

    /// <summary>
    /// Cuántos se piden como «primera página».
    ///
    /// Un número fijo y pequeño, no calculado de cuántos haya: lo que la prueba comprueba es que
    /// la búsqueda alcanza algo que no está en la página que se pidió, y para eso da igual el
    /// tamaño mientras haya registros de sobra.
    /// </summary>
    private const int TAMANO_DE_PAGINA = 3;

    /// <summary>
    /// Una palabra del título que sirva para buscar: la más larga, para que no sea «de» ni «la».
    /// </summary>
    private static string PalabraBuscable(string titulo) =>
        titulo.Split(Separadores, StringSplitOptions.RemoveEmptyEntries)
              .OrderByDescending(p => p.Length)
              .First();

    /// <summary>
    /// El fallo original, con su nombre: encontrar algo que no cabe en la primera página.
    ///
    /// Se pide una página, se elige un registro que <b>no</b> está en ella y se busca. Sin esa
    /// comprobación previa, la prueba pasaría también con la búsqueda vieja del cliente y no diría
    /// nada.
    ///
    /// Ha fallado de dos maneras distintas antes de quedar así, y las dos por suponer cosas sobre
    /// los datos: primero dando por hecho que el sembrador crea más de cinco —contra el contenedor,
    /// que tiene cinco proyectos justos, fallaba sin que nada estuviera roto—, y después calculando
    /// el tamaño de página de un conteo anterior, que otras pruebas de la misma colección movían
    /// entre las dos llamadas.
    /// </summary>
    [Theory]
    [MemberData(nameof(Listas))]
    public async Task Encuentra_registros_que_no_estan_en_la_primera_pagina(string ruta, string campo)
    {
        var cliente = await AutenticarAsync();

        var todos = await ItemsAsync(cliente, $"{ruta}?pageSize=200");
        todos.Length.Should().BeGreaterThan(TAMANO_DE_PAGINA,
            "hacen falta más registros que los que cabe en una página para que haya algo fuera");

        // Se pide la página primero y **se elige después** uno que no esté en ella.
        //
        // Al revés era frágil: la primera versión calculaba el tamaño de página a partir de un
        // conteo anterior, y otras pruebas de la misma colección crean tareas mientras tanto. Con
        // una fila más entre las dos llamadas, el elemento elegido entraba en la página y la
        // prueba fallaba sin que nada estuviera roto. Comprobar la pertenencia real no depende de
        // cuántos haya.
        var primeraPagina = await ItemsAsync(cliente, $"{ruta}?pageSize={TAMANO_DE_PAGINA}");
        var enLaPagina = primeraPagina.Select(i => i.GetProperty("id").GetGuid()).ToHashSet();

        var escondido = todos.LastOrDefault(t => !enLaPagina.Contains(t.GetProperty("id").GetGuid()));

        escondido.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "el elemento elegido tiene que estar fuera de la primera página; si no, buscar en el "
            + "cliente sobre lo ya descargado también lo habría encontrado");

        var id = escondido.GetProperty("id").GetGuid();
        var titulo = escondido.GetProperty(campo).GetString()!;

        var encontrados = await ItemsAsync(cliente, $"{ruta}?pageSize=200&search={Uri.EscapeDataString(titulo)}");

        encontrados.Select(i => i.GetProperty("id").GetGuid()).Should().Contain(id,
            $"«{titulo}» existe en el inquilino, así que buscarlo tiene que darlo aunque esté en "
            + "la última página");
    }

    /// <summary>
    /// Que además de encontrar, descarte: una búsqueda que devuelve la lista entera es el mismo
    /// engaño que el menú que filtraba y no filtraba.
    /// </summary>
    [Theory]
    [MemberData(nameof(Listas))]
    public async Task Lo_que_devuelve_contiene_lo_buscado_y_es_menos_que_todo(string ruta, string campo)
    {
        var cliente = await AutenticarAsync();

        var todos = await ItemsAsync(cliente, $"{ruta}?pageSize=200");
        var palabra = PalabraBuscable(todos[^1].GetProperty(campo).GetString()!);

        var encontrados = await ItemsAsync(cliente, $"{ruta}?pageSize=200&search={Uri.EscapeDataString(palabra)}");

        encontrados.Should().NotBeEmpty("la palabra sale de un registro que existe");
        encontrados.Length.Should().BeLessThan(todos.Length,
            $"«{palabra}» no puede estar en todos los registros; si lo devuelve todo, el parámetro "
            + "llega y nadie lo lee");

        // El texto puede estar en el título o en la descripción: se busca en los dos, así que
        // exigir que esté en el título convertiría un acierto en un fallo de la prueba.
        foreach (var item in encontrados)
        {
            var titulo = item.GetProperty(campo).GetString() ?? "";
            var descripcion = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

            (Plano(titulo) + " " + Plano(descripcion)).Should().Contain(Plano(palabra),
                "cada resultado tiene que contener lo buscado en alguna parte");
        }
    }

    /// <summary>
    /// Sin <c>search</c>, nada cambia. Es la comprobación aburrida que evita que añadir la
    /// búsqueda rompa las listas de siempre, y que un cuadro a medio escribir —espacios— se lea
    /// como «no encuentres nada».
    /// </summary>
    [Theory]
    [MemberData(nameof(Listas))]
    public async Task Sin_buscar_o_buscando_en_blanco_sale_la_lista_de_siempre(string ruta, string campo)
    {
        _ = campo;
        var cliente = await AutenticarAsync();

        var sinParametro = await ItemsAsync(cliente, $"{ruta}?pageSize=200");
        var enBlanco = await ItemsAsync(cliente, $"{ruta}?pageSize=200&search=%20%20");

        enBlanco.Length.Should().Be(sinParametro.Length,
            "buscar espacios no es buscar; sería la lista vacía en cuanto alguien deje el cuadro a medias");
    }

    /// <summary>
    /// Las personas también, porque es la lista que alimenta las menciones con <c>@</c> y la que
    /// estaba rota: el servicio pedía una ruta que no existe y el error se perdía por el camino.
    /// </summary>
    [Fact]
    public async Task Las_personas_se_buscan_por_nombre_y_por_correo()
    {
        var cliente = await AutenticarAsync();

        var todas = await ItemsAsync(cliente, "/api/v1/users");
        todas.Should().NotBeEmpty("el sembrador crea usuarios");

        var alguien = todas[^1];
        var nombre = alguien.GetProperty("name").GetString()!;
        var correo = alguien.GetProperty("email").GetString()!;
        var id = alguien.GetProperty("id").GetGuid();

        var porNombre = await ItemsAsync(cliente, $"/api/v1/users?search={Uri.EscapeDataString(PalabraBuscable(nombre))}");
        porNombre.Select(u => u.GetProperty("id").GetGuid()).Should().Contain(id);

        var porCorreo = await ItemsAsync(cliente, $"/api/v1/users?search={Uri.EscapeDataString(correo)}");
        porCorreo.Select(u => u.GetProperty("id").GetGuid()).Should().Contain(id,
            "quien escribe el correo entero espera a esa persona, no la lista completa");
    }

    /// <summary>
    /// La lista de personas puede recortarse, y sin pedirlo no se recorta.
    ///
    /// El tope se añadió al ver la base de desarrollo: <c>/users</c> devuelve seiscientas ochenta
    /// y tres filas de una vez, y el desplegable de menciones se las descargaba todas para enseñar
    /// cinco. Lo segundo que comprueba importa igual: la pantalla de administración pide esta
    /// misma lista y un recorte por defecto le escondería personas sin decírselo a nadie.
    /// </summary>
    [Fact]
    public async Task La_lista_de_personas_se_recorta_solo_si_se_pide()
    {
        var cliente = await AutenticarAsync();

        var todas = await ItemsAsync(cliente, "/api/v1/users");
        todas.Length.Should().BeGreaterThan(1, "con una sola persona no se distingue recortar de no recortar");

        var recortada = await ItemsAsync(cliente, "/api/v1/users?pageSize=1");
        recortada.Should().ContainSingle("se ha pedido una y sólo una");

        var sinPedirlo = await ItemsAsync(cliente, "/api/v1/users");
        sinPedirlo.Length.Should().Be(todas.Length,
            "sin `pageSize` siguen viniendo todas; recortar por defecto escondería personas en la "
            + "pantalla de administración sin que nadie se enterara");
    }

    /// <summary>
    /// «DISENO» tiene que encontrar «Diseño».
    ///
    /// Es lo que hacía a mano el buscador del cliente y se quitó al mover la búsqueda al servidor.
    /// Si algún día la base de datos cambia de colación, esto se entera; sin la prueba, el síntoma
    /// sería que las tildes dejan de encontrarse y nadie sabría por qué.
    /// </summary>
    [Fact]
    public async Task Buscar_ignora_tildes_y_mayusculas()
    {
        var cliente = await AutenticarAsync();

        var todas = await ItemsAsync(cliente, "/api/v1/tasks?pageSize=200");

        var conTilde = todas
            .Select(t => t.GetProperty("title").GetString() ?? "")
            .SelectMany(PalabrasDe)
            .FirstOrDefault(p => Plano(p) != p.ToLowerInvariant());

        // Sin datos acentuados no hay nada que comprobar, y fingirlo creando aquí una tarea con
        // tilde probaría la colación de una fila recién insertada, no la de las que ya existen.
        if (conTilde is null) return;

        var buscado = Plano(conTilde).ToUpperInvariant();
        var encontradas = await ItemsAsync(cliente, $"/api/v1/tasks?pageSize=200&search={Uri.EscapeDataString(buscado)}");

        encontradas.Should().NotBeEmpty(
            $"«{buscado}» tiene que encontrar «{conTilde}»: la colación de la base de datos es "
            + "insensible a tildes y mayúsculas");
    }

    private static IEnumerable<string> PalabrasDe(string texto) =>
        texto.Split(Separadores, StringSplitOptions.RemoveEmptyEntries).Where(p => p.Length > 3);
}
