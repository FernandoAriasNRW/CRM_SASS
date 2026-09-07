using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El constructor de informes, contra la API levantada.
///
/// <b>La prueba que da sentido a las demás es la del contrato:</b> todo lo que el catálogo
/// ofrece, el motor sabe resolverlo. Es la unión que este módulo ya rompió dos veces —el
/// desplegable de tipos y el filtro del menú— y aquí el daño sería peor, porque la definición se
/// guarda: el fallo aparecería al exportar, días después.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ConstructorDeInformesFlowTests(CrmApiFactory factory)
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
            Name = $"A medida {Guid.NewGuid():N}"[..30],
            Type = "Custom",
            Format = "Csv"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static object Definicion(
        string origen = "Tareas", string agrupacion = "estado", string medida = "conteo",
        string forma = "tabla", object[]? filtros = null, string? granularidad = null)
        => new { origen, agrupacion, medida, forma, filtros = filtros ?? [], granularidad };

    #region El catálogo

    [Fact]
    public async Task El_catalogo_trae_origenes_campos_medidas_y_operadores()
    {
        var cliente = await AutenticarAsync();

        var catalogo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/catalogo");

        catalogo.GetProperty("origenes").EnumerateArray().Should().NotBeEmpty();
        catalogo.GetProperty("operadores").EnumerateArray().Should().NotBeEmpty();
        catalogo.GetProperty("formas").EnumerateArray().Should().NotBeEmpty();
        catalogo.GetProperty("granularidades").EnumerateArray().Should().NotBeEmpty();

        var tareas = catalogo.GetProperty("origenes").EnumerateArray()
            .Single(o => o.GetProperty("clave").GetString() == "Tareas");

        tareas.GetProperty("campos").EnumerateArray().Should().NotBeEmpty();
        tareas.GetProperty("medidas").EnumerateArray().Should().NotBeEmpty();
    }

    /// <summary>
    /// <b>Todo lo que el catálogo ofrece, el motor lo sabe resolver.</b>
    ///
    /// Recorre cada origen, cada campo y cada medida y pide la vista previa. Si alguna combinación
    /// no estuviera implementada, la pantalla la ofrecería igualmente —porque se alimenta del
    /// catálogo— y el informe fallaría al generarse, cuando quien lo construyó ya no está.
    ///
    /// Es la misma comprobación que <c>ContratoDeInformesTests</c> hace con los tipos de informe,
    /// llevada a una superficie veinte veces mayor.
    /// </summary>
    [Fact]
    public async Task Todo_lo_que_el_catalogo_ofrece_se_puede_calcular()
    {
        var cliente = await AutenticarAsync();
        var catalogo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/catalogo");

        var fallos = new List<string>();

        foreach (var origen in catalogo.GetProperty("origenes").EnumerateArray())
        {
            var clave = origen.GetProperty("clave").GetString()!;

            foreach (var campo in origen.GetProperty("campos").EnumerateArray())
            {
                var campoClave = campo.GetProperty("clave").GetString()!;
                var esFecha = campo.GetProperty("tipo").GetString() == "Fecha";

                foreach (var medida in origen.GetProperty("medidas").EnumerateArray())
                {
                    var medidaClave = medida.GetProperty("clave").GetString()!;

                    var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports/vista-previa", new
                    {
                        definicion = Definicion(clave, campoClave, medidaClave,
                                                granularidad: esFecha ? "mes" : null),
                        titulo = "Contrato"
                    });

                    if (respuesta.StatusCode != HttpStatusCode.OK)
                    {
                        fallos.Add($"{clave}/{campoClave}/{medidaClave}: "
                                   + await respuesta.Content.ReadAsStringAsync());
                    }
                }
            }
        }

        fallos.Should().BeEmpty(
            "el catálogo es lo que la pantalla ofrece; lo que ofrece tiene que poder calcularse");
    }

    /// <summary>
    /// Y cada operador se puede aplicar a cada campo de su tipo.
    ///
    /// El catálogo dice qué operadores valen para qué tipos; esta prueba comprueba que el motor
    /// los traduce todos. Un operador que la pantalla ofrece y el motor no traduce produce un
    /// informe que revienta al filtrar.
    /// </summary>
    [Fact]
    public async Task Todos_los_operadores_se_pueden_aplicar_a_los_campos_de_su_tipo()
    {
        var cliente = await AutenticarAsync();
        var catalogo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/catalogo");

        var operadores = catalogo.GetProperty("operadores").EnumerateArray().ToList();
        var fallos = new List<string>();

        foreach (var origen in catalogo.GetProperty("origenes").EnumerateArray())
        {
            var clave = origen.GetProperty("clave").GetString()!;
            var agrupacion = origen.GetProperty("campos").EnumerateArray().First();
            var agrupacionClave = agrupacion.GetProperty("clave").GetString()!;
            var agrupacionEsFecha = agrupacion.GetProperty("tipo").GetString() == "Fecha";

            foreach (var campo in origen.GetProperty("campos").EnumerateArray())
            {
                var campoClave = campo.GetProperty("clave").GetString()!;
                var tipo = campo.GetProperty("tipo").GetString()!;

                // Los operadores **de este campo**, que el catálogo resuelve: el tipo no basta,
                // porque «está vacío» sólo vale sobre campos que pueden no tener valor.
                var suyos = campo.GetProperty("operadores").EnumerateArray()
                    .Select(o => o.GetString()).ToHashSet();

                foreach (var operador in operadores.Where(o => suyos.Contains(o.GetProperty("clave").GetString())))
                {
                    var opClave = operador.GetProperty("clave").GetString()!;

                    // Si el campo es una lista cerrada, se usa uno de sus valores reales. Antes
                    // se mandaba una cadena cualquiera y el servidor la rechazaba —con razón—,
                    // que es lo que destapó que la pantalla obligaba a escribir «Open» a mano.
                    var valores = campo.GetProperty("valores").EnumerateArray()
                        .Select(v => v.GetString()!).ToList();

                    var valor = operador.GetProperty("necesitaValor").GetBoolean()
                        ? valores.FirstOrDefault() ?? ValorDePrueba(tipo)
                        : null;

                    var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports/vista-previa", new
                    {
                        definicion = Definicion(
                            clave, agrupacionClave, "conteo",
                            filtros: [new { campo = campoClave, operador = opClave, valor }],
                            granularidad: agrupacionEsFecha ? "mes" : null),
                        titulo = "Contrato de operadores"
                    });

                    if (respuesta.StatusCode != HttpStatusCode.OK)
                    {
                        fallos.Add($"{clave}/{campoClave} ({tipo}) {opClave}: "
                                   + await respuesta.Content.ReadAsStringAsync());
                    }
                }
            }
        }

        fallos.Should().BeEmpty();
    }

    /// <summary>Un valor plausible según el tipo, para que el filtro se pueda evaluar.</summary>
    private static string ValorDePrueba(string tipo) => tipo switch
    {
        "Numero" => "1",
        "Fecha" => "2020-01-01",
        "Persona" or "Referencia" => Guid.Empty.ToString(),
        _ => "algo"
    };

    #endregion

    #region Vista previa

    [Fact]
    public async Task La_vista_previa_devuelve_columnas_y_filas()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports/vista-previa", new
        {
            definicion = Definicion("Tareas", "estado", "conteo"),
            titulo = "Tareas por estado"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var previa = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

        previa.GetProperty("columnas").EnumerateArray().Should().HaveCount(2);
        previa.GetProperty("filas").EnumerateArray().Should().NotBeEmpty("el sembrador crea tareas");
        previa.GetProperty("subtitulo").GetString().Should().Contain("Tareas");
    }

    /// <summary>
    /// El subtítulo dice qué filtros se aplicaron.
    ///
    /// Un informe exportado circula por correo y acaba en una reunión sin quien lo generó
    /// delante. Sin esta línea, nadie puede distinguir «tickets por estado» de «tickets por
    /// estado, sólo los urgentes».
    /// </summary>
    [Fact]
    public async Task El_subtitulo_deja_constancia_de_los_filtros()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports/vista-previa", new
        {
            definicion = Definicion("Tickets", "estado", "conteo",
                filtros: [new { campo = "prioridad", operador = "es", valor = "High" }]),
            titulo = "Tickets urgentes"
        });

        var previa = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

        previa.GetProperty("subtitulo").GetString().Should().Contain("Prioridad").And.Contain("High");
    }

    /// <summary>Filtrar de verdad cambia el resultado; si no, el filtro sería un adorno.</summary>
    [Fact]
    public async Task Un_filtro_cambia_el_resultado()
    {
        var cliente = await AutenticarAsync();

        var sinFiltro = await Total(cliente, Definicion("Tickets", "estado", "conteo"));

        var conFiltro = await Total(cliente, Definicion("Tickets", "estado", "conteo",
            filtros: [new { campo = "prioridad", operador = "es", valor = "High" }]));

        sinFiltro.Should().BeGreaterThan(0);
        conFiltro.Should().BeLessThan(sinFiltro, "filtrar por una prioridad deja fuera las demás");
    }

    private static async Task<int> Total(HttpClient cliente, object definicion)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports/vista-previa",
            new { definicion, titulo = "x" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var previa = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

        // Se suman los valores de la segunda columna: es la medida.
        return previa.GetProperty("filas").EnumerateArray()
            .Sum(f => int.Parse(f.EnumerateArray().Last().GetString()!));
    }

    /// <summary>
    /// Una media sin nada que promediar escribe una raya, no un cero.
    ///
    /// «0 días medios hasta resolver» dice que se resuelve al instante; la verdad, cuando no hay
    /// ningún ticket resuelto, es que no hay nada que medir. Son dos afirmaciones distintas y sólo
    /// una es cierta.
    ///
    /// Este proyecto ya tuvo esta mentira exacta en el tiempo medio de entrega del panel, que
    /// devolvía 0 en vez de un hueco. Se arregló allí y volvía a colarse aquí.
    /// </summary>
    [Fact]
    public async Task Una_media_sin_datos_escribe_una_raya_y_no_un_cero()
    {
        var cliente = await AutenticarAsync();

        // Se piden los días medios hasta resolver **de los tickets sin resolver**: por definición
        // no hay ninguno que promediar.
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports/vista-previa", new
        {
            definicion = Definicion("Tickets", "estado", "media_dias_resolucion",
                filtros: [new { campo = "resolucion", operador = "vacio", valor = (string?)null }]),
            titulo = "Sin resolver"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var previa = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var filas = previa.GetProperty("filas").EnumerateArray().ToList();

        filas.Should().NotBeEmpty("hay tickets sin resolver");

        foreach (var fila in filas)
        {
            fila.EnumerateArray().Last().GetString().Should().Be("—",
                "sin nada que promediar, un cero diría que se resuelve al instante");
        }
    }

    [Fact]
    public async Task Una_definicion_invalida_dice_que_falla_y_que_vale()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports/vista-previa", new
        {
            definicion = Definicion("Tareas", "agente", "conteo"),
            titulo = "Imposible"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var mensaje = await respuesta.Content.ReadAsStringAsync();
        mensaje.Should().Contain("agente", "hay que decir qué se pidió");
        mensaje.Should().Contain("estado", "y qué se podría haber pedido");
    }

    #endregion

    #region Guardar y exportar

    /// <summary>
    /// El recorrido completo: construir, guardar y exportar.
    ///
    /// Antes de esto, un informe de tipo «Custom» se rechazaba al exportar diciendo que el
    /// constructor no existía. Ahora existe, y lo que se guarda es lo que se exporta.
    /// </summary>
    [Fact]
    public async Task Un_informe_a_medida_se_guarda_y_se_exporta_con_lo_que_dice_su_definicion()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var guardado = await cliente.PutAsJsonAsync(
            $"/api/v1/reports/{informeId}/definicion", Definicion("Tickets", "prioridad", "conteo", "barras"));

        guardado.StatusCode.Should().Be(HttpStatusCode.NoContent, await guardado.Content.ReadAsStringAsync());

        // Se relee en otra petición: es la lección del PATCH que respondía 200 sin guardar.
        var leida = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{informeId}/definicion");
        leida.GetProperty("origen").GetString().Should().Be("Tickets");
        leida.GetProperty("agrupacion").GetString().Should().Be("prioridad");
        leida.GetProperty("forma").GetString().Should().Be("barras");

        // Lo que se lee es exactamente lo que se guarda: sin propiedades calculadas coladas.
        // `filtrosAplicados` y `gruposEfectivos` son atajos de lectura del dominio y salían en la
        // respuesta, así que reenviar ese JSON mandaba campos que el servidor ignora.
        leida.TryGetProperty("filtrosAplicados", out _).Should().BeFalse();
        leida.TryGetProperty("gruposEfectivos", out _).Should().BeFalse();

        // Y el fichero exportado trae esa agrupación, no otra.
        var peticion = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Csv", null);
        peticion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var exportacionId = (await peticion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var final = await EsperarAsync(cliente, exportacionId);

        final.GetProperty("estado").GetString().Should().Be("Lista",
            "motivo: " + (final.TryGetProperty("error", out var e) ? e.ToString() : "ninguno"));

        var descarga = await cliente.GetAsync($"/api/v1/exportaciones/{exportacionId}/descargar");
        var texto = Encoding.UTF8.GetString(await descarga.Content.ReadAsByteArrayAsync());

        texto.Should().Contain("Prioridad", "la columna sale de la agrupación que se guardó");
        texto.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCountGreaterThan(1);
    }

    /// <summary>
    /// Guardar una definición imposible se rechaza **al guardar**, no al exportar.
    ///
    /// Es la razón de que la validación esté en el dominio y no sólo en el borde: un informe roto
    /// que se guarda bien falla más tarde, cuando quien lo construyó ya no está mirando.
    /// </summary>
    [Fact]
    public async Task Una_definicion_imposible_no_se_llega_a_guardar()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/v1/reports/{informeId}/definicion",
            Definicion("Proyectos", "estado", "suma_horas"));

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("suma_horas");
    }

    /// <summary>
    /// Un informe a medida sin definición lo dice, en vez de exportar un fichero vacío.
    ///
    /// Un PDF con encabezados y nada dentro parece un fallo del sistema; esto es un informe a
    /// medio configurar, que es otra cosa y se arregla de otra manera.
    /// </summary>
    [Fact]
    public async Task Un_informe_a_medida_sin_definicion_lo_dice()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var peticion = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Csv", null);
        var exportacionId = (await peticion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var final = await EsperarAsync(cliente, exportacionId);

        final.GetProperty("estado").GetString().Should().Be("Fallida");
        final.GetProperty("error").GetString().Should().Contain("constructor");
    }

    #endregion

    private static async Task<JsonElement> EsperarAsync(HttpClient cliente, Guid exportacionId)
    {
        var limite = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < limite)
        {
            var estado = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/exportaciones/{exportacionId}");
            if (estado.GetProperty("estado").GetString() is "Lista" or "Fallida") return estado;

            await Task.Delay(500);
        }

        throw new TimeoutException($"La exportación {exportacionId} no terminó a tiempo");
    }
}
