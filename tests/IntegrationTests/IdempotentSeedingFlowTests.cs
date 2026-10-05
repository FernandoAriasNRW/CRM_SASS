using System.Net.Http.Json;
using System.Text.Json;
using ApiHost.Services;
using FluentAssertions;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que sembrar dos veces deje lo mismo que sembrar una.
///
/// <b>Nace de un fallo medido, no de una buena práctica.</b> Dos sitios buscaban al administrador
/// con el filtro de inquilino puesto —el arranque de <c>Program</c> y el propio sembrador—, y en
/// ese momento no hay petición, así que el inquilino vale <c>Guid.Empty</c> y la búsqueda no
/// encuentra a nadie **aunque la tabla esté llena**. Cada arranque creaba otro «admin@acme.com».
/// La base de desarrollo llegó a 695 usuarios con 11 correos distintos.
///
/// Y no se quedaba en desorden: el inicio de sesión busca por correo y se queda con una fila
/// cualquiera, de modo que quien entraba no era el administrador que posee los proyectos.
/// <b>«Mis proyectos» enseñaba 0 teniendo cinco.</b> Nadie lo relacionaba con el sembrador.
///
/// Por eso se comprueba el número de filas antes y después, y no que el método «termine bien»:
/// terminaba bien las 115 veces.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class IdempotentSeedingFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    /// <summary>
    /// Cuenta los usuarios <b>sin filtros</b>: es la única forma de ver los que el filtro de
    /// inquilino escondía, que son justo los que sobraban.
    /// </summary>
    private static Task<int> UserCountAsync(IServiceProvider provider)
    {
        var identityDb = provider.GetRequiredService<IdentityDbContext>();
        return identityDb.User.IgnoreQueryFilters().CountAsync();
    }

    [Fact]
    public async Task Seeding_twice_creates_no_extra_users()
    {
        using var scope = factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeederService>();

        // La API ya sembró al arrancar, así que esta es al menos la segunda pasada.
        var before = await UserCountAsync(scope.ServiceProvider);
        before.Should().BeGreaterThan(0, "la API siembra al arrancar; sin datos esto no comprueba nada");

        await seeder.SeedAllAsync();
        var after = await UserCountAsync(scope.ServiceProvider);

        after.Should().Be(before,
            "sembrar sobre una base ya sembrada no debe añadir usuarios. Cuando esto fallaba, "
            + "cada arranque metía once filas más y la base de desarrollo llegó a 695");
    }

    /// <summary>
    /// La comprobación que de verdad importa: ningún correo repetido.
    ///
    /// El contador de arriba pasaría también si el sembrador borrase uno y creara otro; esto no.
    /// Además es lo que el índice único de la base garantiza, así que si algún día alguien lo
    /// quita, esta prueba lo cuenta.
    /// </summary>
    [Fact]
    public async Task No_email_is_repeated()
    {
        using var scope = factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var emails = await identityDb.User
            .IgnoreQueryFilters()
            .Select(u => u.Email.Value)
            .ToListAsync();

        emails.Should().OnlyHaveUniqueItems(
            "el correo identifica a la persona al entrar, y el índice único de la base lo impone");
    }

    /// <summary>
    /// Y la consecuencia visible, que es por donde se notó: quien entra tiene que ser alguien con
    /// sus cosas, no un homónimo vacío.
    ///
    /// Se compara «mis proyectos» contra el total y no contra un número escrito aquí: lo que
    /// estaba roto no era la cuenta, era que el usuario del token no era el propietario de nada.
    /// </summary>
    [Fact]
    public async Task Who_signs_in_is_the_admin_who_owns_the_data()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var all = await client.GetFromJsonAsync<JsonElement>("/api/v1/projects?pageSize=1");
        var mine = await client.GetFromJsonAsync<JsonElement>("/api/v1/projects?pageSize=1&filter=mine");

        all.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0, "el sembrador crea proyectos");

        mine.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0,
            "el sembrador pone al administrador de propietario de los proyectos que crea, así que "
            + "el administrador que inicia sesión tiene que verlos. Con los correos duplicados "
            + "entraba un «admin@acme.com» distinto del que era propietario, y esto daba 0");
    }
}
