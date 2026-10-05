using System.Net;
using ApiHost.Startup;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que nada se siembre ni se abra solo.
///
/// Hasta septiembre de 2026 la demostración se sembraba en cada arranque, también en producción,
/// y una base vacía nacía con <c>admin@acme.com</c> / <c>admin123</c>. Ahora cada cosa se enciende
/// en la configuración (<see cref="SeedingSettings"/>); estas pruebas fijan que, sin decir nada,
/// todo está apagado.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ConfiguredSeedingFlowTests(CrmApiFactory factory)
{
    [Fact]
    public void Without_configuration_nothing_is_seeded_and_no_admin_is_created()
    {
        var settings = SeedingSettings.From(new ConfigurationBuilder().Build());

        settings.SeedOnStartup.Should().BeFalse("sembrar al arrancar se enciende a mano");
        settings.AllowSeedEndpoint.Should().BeFalse("el endpoint de siembra se enciende a mano");
        settings.HasInitialAdmin.Should().BeFalse("no hay administrador con contraseña conocida por defecto");
    }

    [Fact]
    public async Task Without_the_option_the_seed_endpoint_does_not_exist()
    {
        // La misma base, otro host con la opción apagada. El de la colección la enciende.
        await using var off = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("DemoData:SeedOnStartup", "false");
            b.UseSetting("DemoData:AllowSeedEndpoint", "false");
        });

        var response = await off.CreateClient().PostAsync("/api/v1/admin/seed-database", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "sin DemoData:AllowSeedEndpoint la ruta no se registra, en ningún entorno");
    }
}
