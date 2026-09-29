using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Startup;

/// <summary>
/// Lo que se hace con la base de datos antes de servir la primera petición: comprobar el
/// aislamiento por inquilino y aplicar migraciones. Crear el primer administrador y sembrar la
/// demostración sólo si la configuración lo pide (<see cref="SeedingSettings"/>).
/// </summary>
public static class DatabaseInitialization
{
    public static void InitializeDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var dbContexts = new DbContext[]
        {
            services.GetRequiredService<global::Identity.Infrastructure.Persistence.IdentityDbContext>(),
            services.GetRequiredService<global::Teams.Infrastructure.Persistence.TeamsDbContext>(),
            services.GetRequiredService<global::Projects.Infrastructure.Persistence.ProjectsDbContext>(),
            services.GetRequiredService<global::WorkItems.Infrastructure.Persistence.WorkItemsDbContext>(),
            services.GetRequiredService<global::Ticketing.Infrastructure.Persistence.TicketingDbContext>(),
            services.GetRequiredService<global::Notifications.Infrastructure.Persistence.NotificationsDbContext>(),
            services.GetRequiredService<global::Calendar.Infrastructure.Persistence.CalendarDbContext>(),
            services.GetRequiredService<global::Communication.Infrastructure.Persistence.CommunicationsDbContext>(),
            services.GetRequiredService<global::Webhook.Infrastructure.Persistence.WebhookDbContext>(),
            services.GetRequiredService<global::Reporting.Infrastructure.Persistence.ReportingDbContext>(),
            services.GetRequiredService<global::Tags.Infrastructure.Persistence.TagsDbContext>(),
            services.GetRequiredService<global::Docs.Infrastructure.Persistence.DocsDbContext>(),
            services.GetRequiredService<global::CustomFields.Infrastructure.Persistence.CustomFieldsDbContext>(),
            services.GetRequiredService<global::Automations.Infrastructure.Persistence.AutomationsDbContext>(),
            services.GetRequiredService<global::Comments.Infrastructure.CommentsDbContext>(),
            services.GetRequiredService<CrmDbContext>()
        };

        var seeding = SeedingSettings.From(app.Configuration);

        EnsureTenantIsolation(dbContexts);
        ApplyMigrations(dbContexts);
        EnsureAnAdministratorExists(services, seeding);

        if (seeding.SeedOnStartup)
            SeedDemoData(services);

        ProvisionBuiltInTags(services);
        ConvertLegacyTicketTags(services);
    }

    /// <summary>
    /// Antes de tocar la base de datos: comprobar que ninguna entidad se ha quedado fuera del
    /// aislamiento por tenant. Una entidad nueva que olvide ITenantEntity, o un DbContext que olvide
    /// ApplyTenantFilters, devolverían filas de todos los clientes sin lanzar ningún error.
    /// Preferimos no arrancar a servir datos cruzados.
    /// </summary>
    private static void EnsureTenantIsolation(IEnumerable<DbContext> dbContexts)
    {
        var isolationViolations = dbContexts
            .SelectMany(TenantIsolationVerifier.FindViolations)
            .ToList();

        if (isolationViolations.Count > 0)
        {
            throw new InvalidOperationException(
                "Aislamiento multi-tenant incompleto. La aplicación no arranca para evitar fuga de datos entre clientes:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, isolationViolations.Select(v => "  - " + v)));
        }
    }

    /// <summary>
    /// Las migraciones son la única vía por la que cambia el esquema. Hasta agosto de 2026 aquí se
    /// llamaba a EnsureCreated() y a CreateTables() tragándose el error 1050: el esquema se creaba,
    /// pero __EFMigrationsHistory quedaba vacía, así que un campo nuevo no llegaba nunca a una base ya
    /// existente. Ver docs/CONTINUACION.md §1.
    ///
    /// Si esto falla, no se sirve nada: arrancar con el esquema a medias es peor que no arrancar. La
    /// causa habitual es una base creada por el mecanismo anterior, cuyo historial hay que sellar una
    /// vez.
    /// </summary>
    private static void ApplyMigrations(IEnumerable<DbContext> dbContexts)
    {
        foreach (var context in dbContexts)
        {
            try
            {
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"No se pudieron aplicar las migraciones de {context.GetType().Name}. "
                    + "Si esta base se creó con el mecanismo anterior (EnsureCreated), su historial de "
                    + "migraciones está vacío y hay que sellarlo una sola vez: ejecutar "
                    + "scripts/db/sellar-historial-migraciones.sql. Detalle en docs/CONTINUACION.md §1.",
                    ex);
            }
        }
    }

    /// <summary>
    /// Red de seguridad: si no hay ni un usuario, no se podría entrar a arreglar nada.
    ///
    /// **Sólo con un administrador configurado** (<c>InitialAdmin:Email</c> y
    /// <c>InitialAdmin:Password</c>). Antes se creaba siempre <c>admin@acme.com</c> con la
    /// contraseña <c>admin123</c>, en cualquier entorno: una base de producción recién creada
    /// arrancaba con una cuenta de administrador cuya contraseña está escrita en el repositorio.
    /// Sin configuración no se crea nadie y se avisa; si la siembra de demostración está encendida,
    /// ella crea a su administrador de demostración.
    ///
    /// **`IgnoreQueryFilters` no es opcional aquí, y su ausencia costó 695 usuarios.** Esto corre en
    /// el arranque, sin petición y por tanto sin usuario, así que el filtro de inquilino compara
    /// contra `Guid.Empty` y `User.Any()` devolvía **false teniendo once usuarios dentro**. Cada
    /// arranque creaba otro «admin@acme.com». Como el inicio de sesión busca por correo y se queda
    /// con una fila cualquiera, quien entraba no era el administrador que posee los proyectos:
    /// «Mis proyectos» enseñaba 0 teniendo cinco.
    ///
    /// Se descartan los borrados a mano en vez de dejar el filtro de papelera puesto: si el único
    /// administrador está en la papelera, esto tiene que crear uno nuevo —si no, nadie puede entrar
    /// a sacarlo—.
    /// </summary>
    private static void EnsureAnAdministratorExists(IServiceProvider services, SeedingSettings seeding)
    {
        var identity = services.GetRequiredService<global::Identity.Infrastructure.Persistence.IdentityDbContext>();
        if (identity.User.IgnoreQueryFilters().Any(u => !u.IsDeleted))
            return;

        if (!seeding.HasInitialAdmin)
        {
            if (!seeding.SeedOnStartup)
                services.GetRequiredService<ILogger<Program>>().LogWarning(
                    "La base no tiene usuarios y no hay administrador inicial configurado: nadie podrá "
                    + "iniciar sesión. Configura InitialAdmin__Email e InitialAdmin__Password.");
            return;
        }

        // Una contraseña corta aquí es una puerta abierta: se rechaza al arrancar, que es cuando
        // alguien está mirando, y no se crea la cuenta.
        if (seeding.InitialAdminPassword!.Length < SeedingSettings.MinInitialAdminPasswordLength)
            throw new InvalidOperationException(
                $"InitialAdmin:Password debe tener al menos {SeedingSettings.MinInitialAdminPasswordLength} caracteres.");

        var adminRole = global::Identity.Domain.ValueObjects.UserRole.Admin;
        var emailResult = global::Identity.Domain.ValueObjects.Email.Create(seeding.InitialAdminEmail!);
        if (emailResult.IsFailure)
            throw new InvalidOperationException($"InitialAdmin:Email no es un correo válido: {emailResult.Error}");

        var email = emailResult.Value!;
        var password = global::Identity.Domain.ValueObjects.PasswordHash.Create(seeding.InitialAdminPassword);
        var user = global::Identity.Domain.Entities.User.Create(Guid.NewGuid(), "Admin", email, password, adminRole).Value!;
        identity.User.Add(user);
        identity.SaveChanges();
    }

    /// <summary>
    /// Da a cada organización las etiquetas predefinidas que le falten (hitos, negocio, seguridad,
    /// tipo de trabajo, fase de desarrollo).
    ///
    /// <b>Aquí y no en la siembra</b>, porque la siembra de demostración está apagada fuera de
    /// desarrollo y las predefinidas son del producto, no de la demostración. Y en cada arranque
    /// porque no hay un momento «nace una organización» del que colgarlo: las organizaciones salen
    /// del primer administrador o de la siembra, las dos justo antes de esto. Es idempotente.
    ///
    /// Las organizaciones salen de los usuarios con <c>IgnoreQueryFilters</c>: sin petición, el
    /// filtro de inquilino no deja ver ninguno (ver <see cref="EnsureAnAdministratorExists"/>).
    /// </summary>
    private static void ProvisionBuiltInTags(IServiceProvider services)
    {
        try
        {
            var identity = services.GetRequiredService<global::Identity.Infrastructure.Persistence.IdentityDbContext>();
            var tenantIds = identity.User.IgnoreQueryFilters()
                .Where(u => !u.IsDeleted)
                .Select(u => u.TenantId)
                .Distinct()
                .ToList();

            var provisioner = services.GetRequiredService<global::Tags.Application.Abstractions.IBuiltInTagProvisioner>();
            foreach (var tenantId in tenantIds.Where(t => t != Guid.Empty))
                provisioner.ProvisionAsync(tenantId).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Como la siembra: sin etiquetas predefinidas la API sigue siendo útil, así que no se
            // tumba el arranque, pero se registra como error para que se vea.
            services.GetRequiredService<ILogger<Program>>()
                .LogError(ex, "No se pudieron crear las etiquetas predefinidas. La aplicación arranca sin ellas.");
        }
    }

    /// <summary>
    /// Las claves antiguas de los tickets a etiquetas de verdad. Después de aprovisionar, porque sus
    /// destinos son predefinidas. Ver <see cref="Tags.LegacyTicketTagsConverter"/>.
    /// </summary>
    private static void ConvertLegacyTicketTags(IServiceProvider services)
    {
        try
        {
            services.GetRequiredService<Tags.LegacyTicketTagsConverter>().ConvertAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // No tumba el arranque: los tickets siguen funcionando, sólo sin esas etiquetas. Se
            // reintenta en el siguiente arranque, porque la conversión es idempotente.
            services.GetRequiredService<ILogger<Program>>()
                .LogError(ex, "No se pudieron convertir las etiquetas antiguas de los tickets.");
        }
    }

    private static void SeedDemoData(IServiceProvider services)
    {
        try
        {
            var seeder = services.GetRequiredService<Services.DataSeederService>();
            seeder.SeedAllAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Error, no aviso. Un aviso se pierde entre el ruido del arranque, y esto llevaba
            // tiempo fallando sin que nadie lo notara: la aplicación levantaba sin proyectos ni
            // tareas y el panel de informes contaba cero. Sigue sin tumbar el arranque —la API es
            // útil aunque no haya datos de demostración— pero ahora se ve.
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "La siembra de datos de demostración falló. La aplicación arranca sin ellos.");
        }
    }
}
