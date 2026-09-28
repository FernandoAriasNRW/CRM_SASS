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
public sealed class SiembraBajoConfiguracionFlowTests(CrmApiFactory factory)
{
    [Fact]
    public void Sin_configuracion_no_se_siembra_ni_se_crea_administrador()
    {
        var ajustes = SeedingSettings.From(new ConfigurationBuilder().Build());

        ajustes.SeedOnStartup.Should().BeFalse("sembrar al arrancar se enciende a mano");
        ajustes.AllowSeedEndpoint.Should().BeFalse("el endpoint de siembra se enciende a mano");
        ajustes.HasInitialAdmin.Should().BeFalse("no hay administrador con contraseña conocida por defecto");
    }

    [Fact]
    public async Task Sin_la_opcion_el_endpoint_de_siembra_no_existe()
    {
        // La misma base, otro host con la opción apagada. El de la colección la enciende.
        await using var apagado = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("DemoData:SeedOnStartup", "false");
            b.UseSetting("DemoData:AllowSeedEndpoint", "false");
        });

        var respuesta = await apagado.CreateClient().PostAsync("/api/v1/admin/seed-database", null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "sin DemoData:AllowSeedEndpoint la ruta no se registra, en ningún entorno");
    }
}
