using Microsoft.EntityFrameworkCore;
using WorkItems.Domain.Entities;
using WorkItems.Infrastructure.Persistence;

namespace ApiHost.Seeding;

public sealed class WorkItemsSeeder(WorkItemsDbContext workItemsDb) : IModuleSeeder
{
    public string Module => "WorkItems";
    public int Order => 40;

    private static readonly (string Title, string Description, string Status, decimal Hours)[] SampleTasks =
    [
        ("Implementar autenticación JWT con Refresh Token en cookies HttpOnly", "Asegurar que los tokens de refresco se almacenen únicamente en cookies de navegador HttpOnly para prevenir ataques XSS.", "Done", 12),
        ("Diseñar matriz de permisos granulares estilo ClickUp", "Crear componentes de interfaz y comandos backend CQRS para gestionar permisos a nivel de Proyecto, Tarea y Doc.", "In Progress", 16),
        ("Optimizar consultas de base de datos MySQL con índices compuestos", "Agregar índices multicolumna en la tabla EntityPermissions y WorkTasks para acelerar la carga de datos.", "Done", 8),
        ("Integrar editor TipTap para la creación de plantillas de documentos", "Permitir dar formato rico a documentos con encabezados, código, listas y componentes interactivos.", "In Progress", 24),
        ("Configurar suscripciones de Webhook con firmado HMAC-SHA256", "Generar firmas criptográficas por evento enviado para validar autenticidad en clientes receptores.", "In Review", 10),
        ("Crear dashboard de analíticas con gráficos de velocidad de equipo", "Visualizar métricas de tareas completadas, acumulado por estado y rendimiento semanal.", "To Do", 14),
        ("Desplegar ambiente de pruebas en Docker Compose", "Configurar contenedor crm_saas_api y base de datos MySQL 8.0 con variables de entorno de producción.", "Done", 6),
        ("Implementar sistema de notificaciones en tiempo real con SignalR", "Notificar inmediatamente cuando un usuario sea mencionado en una tarea o ticket asignado.", "In Review", 18),
        ("Crear componentes de tabla reutilizables con ordenamiento y filtros", "Construir DataTableComponent con paginación, filtros avanzados y guardado de vistas.", "Done", 20),
        ("Auditar seguridad y control de acceso por roles (Admin, Member, Guest)", "Validar middleware de autorización y guards de rutas en Angular 19.", "In Progress", 12),
        ("Configurar plantillas predeterminadas de Documentos (PRD, SOP, Minutas)", "Incluir plantillas predefinidas listas para usar desde la galería de documentos.", "Done", 8),
        ("Desarrollar vistas guardadas personalizadas (Saved Views) por usuario", "Permitir guardar búsquedas y estados de tablas persistidos en base de datos.", "Done", 10),
        ("Pruebas de carga e integración de microservicios con MediatR", "Ejecutar pruebas automatizadas en los handlers CQRS de Identity, WorkItems y Ticketing.", "To Do", 16),
        ("Documentación de endpoints OpenAPI / Swagger con Scalar UI", "Enriquecer la documentación interactiva de la API accesible en /scalar/v1.", "Done", 4),
        ("Configuración de alertas automáticas para tickets críticos de soporte", "Disparar correos e integración con Slack cuando ingrese un ticket con prioridad Urgent.", "To Do", 8)
    ];

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;

        using var _ = workItemsDb.AsTenant(tenantId);
        try { await workItemsDb.Database.ExecuteSqlAsync($"UPDATE `Tasks` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

        var existingCount = await workItemsDb.Tasks.CountAsync(t => t.TenantId == tenantId, cancellationToken);
        if (existingCount >= 10 || context.Projects.Count == 0)
            return;

        var index = 0;
        foreach (var sample in SampleTasks)
        {
            var project = context.Projects[index % context.Projects.Count];
            index++;

            var assignee = context.AllUsers.Count > 0 ? context.AllUsers[index % context.AllUsers.Count].Id : context.Admin.Id;

            var task = WorkTask.Create(
                tenantId,
                project.Id,
                sample.Title,
                sample.Description,
                assignee,
                context.Admin.Id,
                sample.Hours,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7 + index)));

            if (sample.Status != "To Do")
            {
                task.Move("In Progress");
                if (sample.Status == "In Review") task.Move("In Review");
                else if (sample.Status == "Done") task.Move("Done");
            }

            workItemsDb.Tasks.Add(task);
        }

        await workItemsDb.SaveChangesAsync(cancellationToken);
    }
}
