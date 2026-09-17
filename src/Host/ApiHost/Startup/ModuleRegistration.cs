using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Persistence;
using Calendar.Application.Handlers.Commands;
using Calendar.Infrastructure;
using Calendar.Presentation.Endpoints;
using Comments.Presentation.Endpoints;
using Communication.Infrastructure;
using Communication.Presentation.Endpoints;
using CustomFields.Presentation.Endpoints;
using Docs.Application;
using Docs.Infrastructure;
using Docs.Presentation.Endpoints;
using FluentValidation;
using Identity.Application.Handlers.Commands;
using Identity.Infrastructure;
using Identity.Presentation.Endpoints;
using Automations.Presentation.Endpoints;
using Notifications.Application.Handlers.Commands;
using Notifications.Infrastructure;
using Notifications.Presentation.Endpoints;
using Projects.Application.Handlers.Commands;
using Projects.Infrastructure;
using Projects.Presentation.Endpoints;
using Reporting.Presentation.Endpoints;
using Tags.Infrastructure;
using Tags.Presentation;
using Teams.Presentation.Endpoints;
using Ticketing.Application.Handlers.Commands;
using Ticketing.Infrastructure;
using Ticketing.Presentation.Endpoints;
using Webhook.Application.Handlers;
using Webhook.Infrastructure;
using Webhook.Presentation.Endpoints;
using WorkItems.Application.Handlers.Commands;
using WorkItems.Infrastructure;
using WorkItems.Presentation.Endpoints;

namespace ApiHost.Startup;

/// <summary>
/// Qué módulos forman la aplicación y cómo se conectan entre sí.
///
/// Los adaptadores que cruzan módulos viven en el host porque es el único sitio que conoce a
/// todos: ningún módulo referencia a otro. Cada módulo declara el contrato; el host lo satisface.
/// </summary>
public static class ModuleRegistration
{
    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        // Servicios core: Email, DomainEventDispatcher
        services.AddCoreInfrastructure(configuration);

        // Persistencia de módulos: cada módulo con su DbContext independiente.
        services.AddIdentityInfrastructure(configuration);
        services.AddProjectsInfrastructure(configuration);
        services.AddWorkItemsInfrastructure(configuration);
        services.AddTicketingInfrastructure(configuration);
        services.AddNotificationsInfrastructure(configuration);
        services.AddCalendarInfrastructure(configuration);
        services.AddCommunicationInfrastructure(configuration);
        services.AddWebhookInfrastructure(configuration);
        services.AddTagsInfrastructure(configuration);
        services.AddDocsApplication();
        services.AddDocsInfrastructure(configuration);

        services.AddDatabase(configuration);

        // Presentación: endpoints de API.
        services.AddIdentityPresentation(configuration);
        services.AddProjectsPresentation(configuration);
        services.AddWorkItemsPresentation(configuration);
        services.AddTicketingPresentation(configuration);
        services.AddNotificationsPresentation(configuration);
        services.AddCommunicationPresentation(configuration);
        services.AddCalendarPresentation(configuration);
        services.AddWebhookPresentation(configuration);
        services.AddReportingPresentation(configuration);

        services.AddCrossModuleAdapters();

        services.AddTeamsPresentation(configuration);
        services.AddTagsPresentation(configuration);
        services.AddCustomFieldsPresentation(configuration);
        services.AddAutomationsPresentation(configuration);
        services.AddCommentsPresentation(configuration);
        services.AddDocsPresentation(configuration);

        services.AddCommandsAndQueries();

        services.AddDemoDataSeeding();

        return services;
    }

    /// <summary>Un sembrador por módulo; <see cref="Services.DataSeederService"/> los ordena.</summary>
    private static void AddDemoDataSeeding(this IServiceCollection services)
    {
        services.AddScoped<Seeding.IModuleSeeder, Seeding.IdentitySeeder>();
        services.AddScoped<Seeding.IModuleSeeder, Seeding.TeamsSeeder>();
        services.AddScoped<Seeding.IModuleSeeder, Seeding.ProjectsSeeder>();
        services.AddScoped<Seeding.IModuleSeeder, Seeding.WorkItemsSeeder>();
        services.AddScoped<Seeding.IModuleSeeder, Seeding.DocsSeeder>();
        services.AddScoped<Seeding.IModuleSeeder, Seeding.TicketingSeeder>();
        services.AddScoped<Seeding.IModuleSeeder, Seeding.CalendarSeeder>();
        services.AddScoped<Seeding.IModuleSeeder, Seeding.CommunicationSeeder>();
        services.AddScoped<Seeding.IModuleSeeder, Seeding.NotificationsSeeder>();
        services.AddScoped<Seeding.IModuleSeeder, Seeding.WebhookSeeder>();
        services.AddScoped<Seeding.IModuleSeeder, Seeding.TagsSeeder>();
        services.AddScoped<Services.DataSeederService>();
    }

    private static void AddCrossModuleAdapters(this IServiceCollection services)
    {
        // El panel de informes cruza Projects, WorkItems y Ticketing, así que su implementación vive
        // aquí y no dentro del módulo: ningún módulo referencia a otro. Reporting declara el contrato;
        // el host, que sí conoce a todos, lo satisface. Mismo criterio que PuenteDeAutomatizaciones.
        services.AddScoped<global::Reporting.Application.Abstractions.IDashboardRepository, Reporting.ConsultasDelPanel>();

        // Y la fuente de datos de las exportaciones, por lo mismo: un informe de tareas mira WorkItems y
        // uno de tickets mira Ticketing. Reutiliza ConsultasDelPanel para los agregados, de modo que el
        // PDF y la pantalla dan los mismos números.
        services.AddScoped<Reporting.ConsultasDelPanel>();
        services.AddScoped<Reporting.DatosDelInforme>();

        // La agenda de un día, que junta los eventos con lo que vence ese día en tareas, tickets y
        // proyectos. Vive en el host por lo mismo que las dos de arriba: cruza módulos.
        services.AddScoped<Calendar.AgendaDelDia>();

        // El motor de los informes a medida: traduce la definición neutra que construyó el usuario a
        // filas. Mismo sitio y mismo motivo que lo de arriba.
        services.AddScoped<Reporting.MotorDeInformes>();
        services.AddScoped<global::Reporting.Application.Definiciones.IResolutorDeInformes>(
            sp => sp.GetRequiredService<Reporting.MotorDeInformes>());

        // El trabajador que genera los ficheros. Va en segundo plano porque quien exporta recupera el
        // control enseguida, y porque los informes programados ocurren sin nadie delante: un solo camino
        // para las dos cosas.
        services.AddHostedService<Reporting.GeneradorDeExportaciones>();

        // Y el que dispara los informes programados. No genera nada: deja la exportación pedida y el
        // generador de arriba la recoge, para que un informe programado y uno pedido a mano recorran el
        // mismo camino.
        services.AddScoped<Reporting.CorreosDeDestinatarios>();
        services.AddHostedService<Reporting.PlanificadorDeInformes>();

        // El puente entre las tareas y las automatizaciones vive aquí porque es el unico sitio que
        // conoce a los dos modulos. Ver PuenteDeAutomatizaciones.
        services.AddScoped<global::Automations.Application.Abstractions.IEjecutorDeAcciones, Services.EjecutorDeAccionesDeTareas>();

        // Avisar cruza tres módulos: Automations decide, WorkItems sabe quién tiene la tarea y
        // Notifications entrega. Por eso vive aquí y no dentro de ninguno de los tres.
        services.AddScoped<Services.AvisoDeAutomatizacion>();

        // El disparador por vencimiento no lo levanta un evento —nadie toca la tarea— sino este
        // trabajo, que revisa cada hora qué se acerca a su fecha. Es el que reacciona a que NO ha
        // pasado nada, que es justo lo que no se nota solo.
        services.AddHostedService<Services.VigilanteDeVencimientos>();
    }

    private static void AddCommandsAndQueries(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(LoginCommandHandler).Assembly);           // Identity
            cfg.RegisterServicesFromAssembly(typeof(CreateProjectCommandHandler).Assembly);   // Projects
            cfg.RegisterServicesFromAssembly(typeof(CreateTaskCommandHandler).Assembly);      // WorkItems
            cfg.RegisterServicesFromAssembly(typeof(CreateTicketHandler).Assembly);           // Ticketing
            cfg.RegisterServicesFromAssembly(typeof(CreateNotificationHandler).Assembly);     // Notifications
            cfg.RegisterServicesFromAssembly(typeof(CreateCalendarEventHandler).Assembly);    // Calendar
            cfg.RegisterServicesFromAssembly(typeof(global::Reporting.Application.Handlers.Commands.CreateReportHandler).Assembly);           // Reporting
            cfg.RegisterServicesFromAssembly(typeof(global::Communication.Application.Handlers.Commands.CreateConversationHandler).Assembly); // Communication
            cfg.RegisterServicesFromAssembly(typeof(WebhookEventNotificationHandler).Assembly); // Webhook
            cfg.RegisterServicesFromAssembly(typeof(global::Teams.Application.Commands.CreateTeamCommand).Assembly); // Teams
            cfg.RegisterServicesFromAssembly(typeof(global::CustomFields.Application.Commands.DefineCustomFieldCommand).Assembly); // CustomFields
            cfg.RegisterServicesFromAssembly(typeof(global::Automations.Application.DefineAutomationRuleCommand).Assembly); // Automations
            cfg.RegisterServicesFromAssembly(typeof(global::Comments.Application.AddCommentCommand).Assembly); // Comments

            // Pipeline behavior: valida el request con FluentValidation.
            // Va PRIMERO: no tiene sentido autorizar ni despachar una petición malformada.
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));

            // Pipeline behavior: despacha webhook tras commands IWebhookTriggered
            cfg.AddOpenBehavior(typeof(WebhookDispatchBehavior<,>));

            // Pipeline behavior: Authorization
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
        });

        // Registra todos los IValidator<T> de los ensamblados de módulos, para que
        // ValidationBehavior los encuentre. Sin esto los validadores no se ejecutan nunca.
        services.AddValidatorsFromAssemblies(
        [
            typeof(LoginCommandHandler).Assembly,           // Identity
            typeof(CreateProjectCommandHandler).Assembly,   // Projects
            typeof(CreateTaskCommandHandler).Assembly,      // WorkItems
            typeof(CreateTicketHandler).Assembly,           // Ticketing
            typeof(CreateNotificationHandler).Assembly,     // Notifications
            typeof(CreateCalendarEventHandler).Assembly,    // Calendar
            typeof(global::Reporting.Application.Handlers.Commands.CreateReportHandler).Assembly,
            typeof(global::Communication.Application.Handlers.Commands.CreateConversationHandler).Assembly,
            typeof(global::Teams.Application.Commands.CreateTeamCommand).Assembly
        ], includeInternalTypes: true);
    }
}
