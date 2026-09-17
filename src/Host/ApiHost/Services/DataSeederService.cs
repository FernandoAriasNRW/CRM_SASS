using ApiHost.Seeding;

namespace ApiHost.Services;

/// <summary>
/// Siembra los datos de demostración de todos los módulos, en orden.
///
/// Cada módulo tiene su propio <see cref="IModuleSeeder"/>. Esto sólo los ordena, les pasa lo que
/// dejan los anteriores y lleva la cuenta de los que fallan. Antes era un único método de 700
/// líneas con los doce módulos dentro.
/// </summary>
public sealed class DataSeederService(IServiceProvider serviceProvider, ILogger<DataSeederService> logger)
{
    public async Task SeedAllAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting global data seeding for all modules...");

        using var scope = serviceProvider.CreateScope();
        var seeders = scope.ServiceProvider.GetServices<IModuleSeeder>().OrderBy(s => s.Order).ToList();
        var context = new SeedContext();

        // Qué módulos fallaron. Cada sembrador atrapa su propia excepción para que el fallo de uno
        // no impida sembrar los demás —eso está bien—, pero antes el método terminaba diciendo
        // «completed successfully» pasara lo que pasara. La siembra de Projects llevaba fallando en
        // silencio, así que la aplicación arrancaba sin ningún proyecto ni tarea y el panel de
        // informes contaba cero sin que nadie supiera por qué.
        var failures = new List<string>();

        foreach (var seeder in seeders)
        {
            try
            {
                await seeder.SeedAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error seeding {Module} module data", seeder.Module);
                failures.Add(seeder.Module);
            }

            // Sin inquilino no hay a quién sembrar nada: el primero, Identity, es quien lo fija.
            if (context.TenantId == Guid.Empty)
                break;
        }

        if (failures.Count > 0)
        {
            // Se lanza a propósito. Un entorno de demostración a medio sembrar es un entorno
            // roto, y callarlo sólo traslada el desconcierto a quien abra la pantalla y la vea
            // vacía. Quien llame decide qué hacer con esto.
            throw new InvalidOperationException(
                "La siembra falló en estos módulos: " + string.Join(", ", failures) +
                ". Los errores concretos están más arriba en el registro.");
        }

        if (context.TenantId != Guid.Empty)
            logger.LogInformation("Global data seeding completed successfully across all modules!");
    }
}
