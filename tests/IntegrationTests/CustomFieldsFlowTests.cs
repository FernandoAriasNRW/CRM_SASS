using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Campos personalizados de punta a punta contra la API real.
///
/// Lo que sólo se ve aquí:
///
/// 1. Que el **valor se guarde ya normalizado**. La validación está probada aparte, pero que lo
///    que llega a la base sea de verdad la forma canónica —y no lo que escribió el usuario— sólo
///    se comprueba leyéndolo de vuelta.
/// 2. Que **todas las definiciones lleguen al formulario**, tengan valor o no: si sólo llegaran
///    las rellenas, un campo recién creado no aparecería nunca y nadie podría rellenarlo.
/// 3. Que al **borrar un campo se lleve sus valores**, en lugar de dejar respuestas a una
///    pregunta que ya nadie hace.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CustomFieldsFlowTests(CrmApiFactory factory)
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

    private static async Task<Guid> DefinirAsync(HttpClient cliente, string nombre, string tipo,
        bool obligatorio = false, string[]? opciones = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/custom-fields", new
        {
            nombre,
            tipo,
            entidadDestino = "Tarea",
            obligatorio,
            opciones,
            posicion = 0
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> PonerValorAsync(HttpClient cliente, Guid campo, Guid entidad, string? valor)
        => cliente.PutAsJsonAsync($"/api/v1/custom-fields/values/{campo}/{entidad}", new { valor });

    private static async Task<JsonElement> ValoresDeAsync(HttpClient cliente, Guid entidad)
        => await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/custom-fields/values/Tarea/{entidad}");

    [Fact]
    public async Task Un_campo_se_define_y_aparece_en_el_listado()
    {
        var cliente = await AutenticarAsync();
        var nombre = $"Cliente {Guid.NewGuid():N}";

        var id = await DefinirAsync(cliente, nombre, "Texto");

        var lista = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/custom-fields?entidad=Tarea");
        lista.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).Should().Contain(id);
    }

    [Fact]
    public async Task No_se_admiten_dos_campos_con_el_mismo_nombre_para_la_misma_entidad()
    {
        var cliente = await AutenticarAsync();
        var nombre = $"Repetido {Guid.NewGuid():N}";
        await DefinirAsync(cliente, nombre, "Texto");

        var segunda = await cliente.PostAsJsonAsync("/api/v1/custom-fields", new
        {
            nombre, tipo = "Texto", entidadDestino = "Tarea", obligatorio = false, opciones = (string[]?)null, posicion = 0
        });

        segunda.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain("Ya hay un campo");
    }

    [Fact]
    public async Task El_numero_llega_a_la_base_normalizado()
    {
        var cliente = await AutenticarAsync();
        var campo = await DefinirAsync(cliente, $"Importe {Guid.NewGuid():N}", "Numero");
        var entidad = Guid.NewGuid();

        // Se escribe con coma decimal, como en español.
        (await PonerValorAsync(cliente, campo, entidad, "1234,56")).StatusCode.Should().Be(HttpStatusCode.OK);

        var valores = await ValoresDeAsync(cliente, entidad);
        var guardado = valores.EnumerateArray().Single(v => v.GetProperty("definitionId").GetGuid() == campo);

        guardado.GetProperty("valor").GetString().Should().Be("1234.56", "se guarda con punto para poder ordenar y sumar");
    }

    [Fact]
    public async Task Un_valor_que_no_encaja_con_el_tipo_se_rechaza()
    {
        var cliente = await AutenticarAsync();
        var campo = await DefinirAsync(cliente, $"Fecha {Guid.NewGuid():N}", "Fecha");

        var respuesta = await PonerValorAsync(cliente, campo, Guid.NewGuid(), "el martes");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("fecha");
    }

    [Fact]
    public async Task La_seleccion_multiple_se_guarda_en_el_orden_de_la_definicion()
    {
        var cliente = await AutenticarAsync();
        var campo = await DefinirAsync(cliente, $"Colores {Guid.NewGuid():N}", "SeleccionMultiple",
            opciones: ["Rojo", "Verde", "Azul"]);
        var entidad = Guid.NewGuid();

        await PonerValorAsync(cliente, campo, entidad, "Azul\nRojo");

        var valores = await ValoresDeAsync(cliente, entidad);
        valores.EnumerateArray().Single(v => v.GetProperty("definitionId").GetGuid() == campo)
            .GetProperty("valor").GetString().Should().Be("Rojo\nAzul");
    }

    [Fact]
    public async Task Los_campos_sin_valor_tambien_llegan_al_formulario()
    {
        var cliente = await AutenticarAsync();
        var campo = await DefinirAsync(cliente, $"Sin rellenar {Guid.NewGuid():N}", "Texto");
        var entidad = Guid.NewGuid();

        var valores = await ValoresDeAsync(cliente, entidad);

        var elCampo = valores.EnumerateArray().SingleOrDefault(v => v.GetProperty("definitionId").GetGuid() == campo);
        elCampo.ValueKind.Should().NotBe(JsonValueKind.Undefined, "un campo nuevo tiene que poder rellenarse");
        elCampo.GetProperty("valor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Guardar_dos_veces_el_mismo_campo_actualiza_en_lugar_de_duplicar()
    {
        var cliente = await AutenticarAsync();
        var campo = await DefinirAsync(cliente, $"Una vez {Guid.NewGuid():N}", "Texto");
        var entidad = Guid.NewGuid();

        await PonerValorAsync(cliente, campo, entidad, "primero");
        await PonerValorAsync(cliente, campo, entidad, "segundo");

        var valores = await ValoresDeAsync(cliente, entidad);
        valores.EnumerateArray().Count(v => v.GetProperty("definitionId").GetGuid() == campo).Should().Be(1);
        valores.EnumerateArray().Single(v => v.GetProperty("definitionId").GetGuid() == campo)
            .GetProperty("valor").GetString().Should().Be("segundo");
    }

    [Fact]
    public async Task Borrar_el_campo_se_lleva_sus_valores()
    {
        var cliente = await AutenticarAsync();
        var campo = await DefinirAsync(cliente, $"Efímero {Guid.NewGuid():N}", "Texto");
        var entidad = Guid.NewGuid();
        await PonerValorAsync(cliente, campo, entidad, "algo");

        var borrado = await cliente.DeleteAsync($"/api/v1/custom-fields/{campo}");
        borrado.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var valores = await ValoresDeAsync(cliente, entidad);
        valores.EnumerateArray().Should().NotContain(v => v.GetProperty("definitionId").GetGuid() == campo);
    }

    [Fact]
    public async Task Un_campo_obligatorio_no_admite_quedarse_vacio()
    {
        var cliente = await AutenticarAsync();
        var campo = await DefinirAsync(cliente, $"Obligatorio {Guid.NewGuid():N}", "Texto", obligatorio: true);

        var respuesta = await PonerValorAsync(cliente, campo, Guid.NewGuid(), "   ");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("obligatorio");
    }

    [Fact]
    public async Task Renombrar_un_campo_no_toca_los_valores_ya_guardados()
    {
        var cliente = await AutenticarAsync();
        var campo = await DefinirAsync(cliente, $"Antes {Guid.NewGuid():N}", "Texto");
        var entidad = Guid.NewGuid();
        await PonerValorAsync(cliente, campo, entidad, "conservado");

        var nuevoNombre = $"Después {Guid.NewGuid():N}";
        var actualizado = await cliente.PutAsJsonAsync($"/api/v1/custom-fields/{campo}", new
        {
            nombre = nuevoNombre, obligatorio = false, opciones = (string[]?)null, posicion = 1
        });
        actualizado.StatusCode.Should().Be(HttpStatusCode.OK);

        var valores = await ValoresDeAsync(cliente, entidad);
        var elCampo = valores.EnumerateArray().Single(v => v.GetProperty("definitionId").GetGuid() == campo);
        elCampo.GetProperty("nombre").GetString().Should().Be(nuevoNombre);
        elCampo.GetProperty("valor").GetString().Should().Be("conservado");
    }
}
