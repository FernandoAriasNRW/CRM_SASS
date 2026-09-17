using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Startup;

/// <summary>
/// Lo que se hace con la base de datos antes de servir la primera petición: comprobar el
/// aislamiento por inquilino, aplicar migraciones, garantizar un administrador y sembrar datos
/// de demostración.
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

        EnsureTenantIsolation(dbContexts);
        ApplyMigrations(dbContexts);
        EnsureAnAdministratorExists(services);
        SeedDemoData(services);
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
    private static void EnsureAnAdministratorExists(IServiceProvider services)
    {
        var identity = services.GetRequiredService<global::Identity.Infrastructure.Persistence.IdentityDbContext>();
        if (identity.User.IgnoreQueryFilters().Any(u => !u.IsDeleted))
            return;

        var adminRole = global::Identity.Domain.ValueObjects.UserRole.Admin;
        var email = global::Identity.Domain.ValueObjects.Email.Create("admin@acme.com").Value!;
        var password = global::Identity.Domain.ValueObjects.PasswordHash.Create("admin123");
        var user = global::Identity.Domain.Entities.User.Create(Guid.NewGuid(), "Admin", email, password, adminRole).Value!;
        identity.User.Add(user);
        identity.SaveChanges();
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
