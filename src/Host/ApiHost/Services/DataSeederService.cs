using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Teams.Infrastructure.Persistence;
using Teams.Domain.Entities;
using Teams.Domain.ValueObjects;
using Projects.Infrastructure.Persistence;
using Projects.Domain.Entities;
using WorkItems.Infrastructure.Persistence;
using WorkItems.Domain.Entities;
using Ticketing.Infrastructure.Persistence;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;
using Docs.Infrastructure.Persistence;
using Docs.Domain.Entities;
using Docs.Domain.ValueObjects;
using Calendar.Infrastructure.Persistence;
using Calendar.Domain.Entities;
using Calendar.Domain.ValueObjects;
using Communication.Infrastructure.Persistence;
using Communication.Domain.Entities;
using Communication.Domain.ValueObjects;
using Notifications.Infrastructure.Persistence;
using Notifications.Domain.Entities;
using Notifications.Domain.ValueObjects;
using Webhook.Infrastructure.Persistence;
using Webhook.Domain.Entities;
using Tags.Infrastructure.Persistence;
using Tags.Domain.Entities;

namespace ApiHost.Services;

public sealed class DataSeederService(IServiceProvider serviceProvider, ILogger<DataSeederService> logger)
{
    public async Task SeedAllAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting global data seeding for all modules...");
        using var scope = serviceProvider.CreateScope();

        // ---------------------------------------------------------------------
        // 1. IDENTITY & USERS
        // ---------------------------------------------------------------------
        Guid tenantId = Guid.Empty;
        User adminUser = null!;
        List<User> allUsers = new();
        List<User> memberUsers = new();

        try
        {
            var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            
            // Se busca en una variable anulable y sólo al final se asigna a adminUser.
            // Asignar directamente el resultado de FirstOrDefaultAsync dejaba la variable
            // marcada como posiblemente nula durante el resto del método, que es de donde
            // salían las 22 advertencias de desreferencia.
            var existingAdmin = await identityDb.User.FirstOrDefaultAsync(u => u.Email.Value == "admin@acme.com", cancellationToken)
                ?? await identityDb.User.FirstOrDefaultAsync(cancellationToken);

            if (existingAdmin is null)
            {
                var adminRole = UserRole.Admin;
                var email = Email.Create("admin@acme.com").Value!;
                var pass = PasswordHash.Create("admin123");
                existingAdmin = User.Create(Guid.NewGuid(), "Admin Administrator", email, pass, adminRole).Value
                    ?? throw new InvalidOperationException("No se pudo crear el usuario administrador del seed.");
                identityDb.User.Add(existingAdmin);
                await identityDb.SaveChangesAsync(cancellationToken);
            }

            // Todo el resto del seed cuelga de este usuario: es el propietario de los
            // proyectos y quien define el tenant.
            adminUser = existingAdmin;

            tenantId = adminUser.TenantId;

            // Align any orphaned Users or EntityPermissions to tenantId
            try
            {
                await identityDb.Database.ExecuteSqlAsync($"UPDATE `User` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
                await identityDb.Database.ExecuteSqlAsync($"UPDATE `EntityPermissions` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
                await identityDb.Database.ExecuteSqlAsync($"UPDATE `SavedViews` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
            }
            catch { }

            var existingUsers = await identityDb.User.Where(u => u.TenantId == tenantId).ToListAsync(cancellationToken);
            if (existingUsers.Count < 5)
            {
                var fakeUsersData = new (string name, string email, UserRole role)[]
                {
                    ("Sofia Arismendi", "sofia.arismendi@acme.com", UserRole.Member),
                    ("Carlos Mendoza", "carlos.mendoza@acme.com", UserRole.Member),
                    ("Lucia Fernandez", "lucia.fernandez@acme.com", UserRole.Member),
                    ("Mateo Gomez", "mateo.gomez@acme.com", UserRole.Member),
                    ("Valentina Rios", "valentina.rios@acme.com", UserRole.Member),
                    ("Alejandro Silva", "alejandro.silva@acme.com", UserRole.Member),
                    ("Elena Torres", "elena.torres@acme.com", UserRole.Member),
                    ("Diego Morales", "diego.guest@external.com", UserRole.Guest),
                    ("Camila Navarro", "camila.guest@external.com", UserRole.Guest),
                    ("Javier Roca", "javier.roca@acme.com", UserRole.Member)
                };

                foreach (var item in fakeUsersData)
                {
                    if (!existingUsers.Any(u => u.Email.Value == item.email))
                    {
                        var emailVal = Email.Create(item.email).Value!;
                        var passVal = PasswordHash.Create("Secure123*");
                        var uRes = User.Create(tenantId, item.name, emailVal, passVal, item.role);
                        if (uRes.IsSuccess && uRes.Value != null)
                        {
                            identityDb.User.Add(uRes.Value);
                        }
                    }
                }
                await identityDb.SaveChangesAsync(cancellationToken);
                existingUsers = await identityDb.User.Where(u => u.TenantId == tenantId).ToListAsync(cancellationToken);
            }

            allUsers = existingUsers;
            memberUsers = allUsers.Where(u => u.Id != adminUser.Id).ToList();

            // Permissions & Saved Views
            var existingPerms = await identityDb.EntityPermissions.Where(p => p.TenantId == tenantId).ToListAsync(cancellationToken);
            if (existingPerms.Count == 0)
            {
                identityDb.EntityPermissions.AddRange(
                    EntityPermission.CreateForRole(tenantId, "Member", "Projects", Guid.Empty, "Edit"),
                    EntityPermission.CreateForRole(tenantId, "Member", "Tasks", Guid.Empty, "Edit"),
                    EntityPermission.CreateForRole(tenantId, "Member", "Docs", Guid.Empty, "Edit"),
                    EntityPermission.CreateForRole(tenantId, "Member", "Webhooks", Guid.Empty, "View"),
                    EntityPermission.CreateForRole(tenantId, "Guest", "Projects", Guid.Empty, "View"),
                    EntityPermission.CreateForRole(tenantId, "Guest", "Tasks", Guid.Empty, "Edit"),
                    EntityPermission.CreateForRole(tenantId, "Guest", "Docs", Guid.Empty, "View")
                );
                await identityDb.SaveChangesAsync(cancellationToken);
            }

            var existingViews = await identityDb.SavedViews.Where(v => v.TenantId == tenantId && v.UserId == adminUser.Id).ToListAsync(cancellationToken);
            if (existingViews.Count == 0)
            {
                identityDb.SavedViews.AddRange(
                    SavedView.Create(adminUser.Id, tenantId, "WorkItems", "Mis Tareas Pendientes", "{\"page\":1,\"pageSize\":25,\"searchTerm\":\"\",\"filters\":{\"status\":\"To Do\"}}", true),
                    SavedView.Create(adminUser.Id, tenantId, "Projects", "Proyectos Activos Q3", "{\"page\":1,\"pageSize\":25,\"searchTerm\":\"\",\"filters\":{}}", false),
                    SavedView.Create(adminUser.Id, tenantId, "Tickets", "Tickets Prioritarios", "{\"page\":1,\"pageSize\":25,\"searchTerm\":\"\",\"filters\":{\"priority\":\"High\"}}", false)
                );
                await identityDb.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding Identity module data");
        }

        if (tenantId == Guid.Empty) return;

        // ---------------------------------------------------------------------
        // 2. TEAMS
        // ---------------------------------------------------------------------
        try
        {
            var teamsDb = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
            try { await teamsDb.Database.ExecuteSqlAsync($"UPDATE `Teams` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

            var existingTeams = await teamsDb.Teams.Where(t => t.TenantId == tenantId).ToListAsync(cancellationToken);
            if (existingTeams.Count == 0)
            {
                var t1 = Team.Create(tenantId, "🚀 Core Engineering", "Desarrollo de microservicios backend C# .NET 9 y frontend Angular 19");
                var t2 = Team.Create(tenantId, "🎨 Product & UI/UX Design", "Diseño de interfaces, componentes ShadCN y experiencia de usuario");
                var t3 = Team.Create(tenantId, "📊 Sales & Customer Success", "Atención a clientes VIP, soporte técnico y crecimiento comercial");
                var t4 = Team.Create(tenantId, "🔒 DevOps & Cloud Infra", "Infraestructura Cloud, despliegues Docker y seguridad de datos");

                t1.AddMember(adminUser.Id, TeamRole.Owner);
                if (memberUsers.Count > 0) t1.AddMember(memberUsers[0].Id, TeamRole.Member);
                if (memberUsers.Count > 1) t1.AddMember(memberUsers[1].Id, TeamRole.Member);

                t2.AddMember(adminUser.Id, TeamRole.Member);
                if (memberUsers.Count > 2) t2.AddMember(memberUsers[2].Id, TeamRole.Owner);
                if (memberUsers.Count > 3) t2.AddMember(memberUsers[3].Id, TeamRole.Member);

                t3.AddMember(adminUser.Id, TeamRole.Member);
                if (memberUsers.Count > 4) t3.AddMember(memberUsers[4].Id, TeamRole.Owner);

                t4.AddMember(adminUser.Id, TeamRole.Owner);
                if (memberUsers.Count > 5) t4.AddMember(memberUsers[5].Id, TeamRole.Member);

                teamsDb.Teams.AddRange(t1, t2, t3, t4);
                await teamsDb.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding Teams module data");
        }

        // ---------------------------------------------------------------------
        // 3. PROJECTS, SPACES & FOLDERS
        // ---------------------------------------------------------------------
        List<Project> existingProjects = new();
        try
        {
            var projectsDb = scope.ServiceProvider.GetRequiredService<ProjectsDbContext>();
            try
            {
                await projectsDb.Database.ExecuteSqlAsync($"UPDATE `Spaces` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
                await projectsDb.Database.ExecuteSqlAsync($"UPDATE `Folders` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
                await projectsDb.Database.ExecuteSqlAsync($"UPDATE `Projects` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
            }
            catch { }

            var existingSpaces = await projectsDb.Spaces.Where(s => s.TenantId == tenantId).ToListAsync(cancellationToken);
            if (existingSpaces.Count == 0)
            {
                var s1 = Space.Create(tenantId, "🚀 Plataforma SaaS Core", "Proyectos principales del motor CRM y microservicios", "#8B5CF6");
                var s2 = Space.Create(tenantId, "💼 Operaciones & Clientes", "Gestión de clientes enterprise, soporte y onboarding", "#3B82F6");
                var s3 = Space.Create(tenantId, "📊 Marketing & Producto", "Lanzamientos Q3, diseño UI/UX y analíticas de uso", "#10B981");

                projectsDb.Spaces.AddRange(s1, s2, s3);
                await projectsDb.SaveChangesAsync(cancellationToken);
                existingSpaces = await projectsDb.Spaces.Where(s => s.TenantId == tenantId).ToListAsync(cancellationToken);
            }

            var spaceCore = existingSpaces.FirstOrDefault(s => s.Name.Contains("Core")) ?? existingSpaces[0];
            var spaceOps = existingSpaces.FirstOrDefault(s => s.Name.Contains("Operaciones")) ?? existingSpaces[0];

            var existingFolders = await projectsDb.Folders.Where(f => f.TenantId == tenantId).ToListAsync(cancellationToken);
            if (existingFolders.Count == 0)
            {
                var f1 = Folder.Create(tenantId, spaceCore.Id, "Backend Microservicios");
                var f2 = Folder.Create(tenantId, spaceCore.Id, "Aplicación Web Angular 19");
                var f3 = Folder.Create(tenantId, spaceOps.Id, "Onboarding Clientes VIP");

                projectsDb.Folders.AddRange(f1, f2, f3);
                await projectsDb.SaveChangesAsync(cancellationToken);
                existingFolders = await projectsDb.Folders.Where(f => f.TenantId == tenantId).ToListAsync(cancellationToken);
            }

            var folderBackend = existingFolders.FirstOrDefault(f => f.Name.Contains("Backend")) ?? existingFolders[0];
            var folderFrontend = existingFolders.FirstOrDefault(f => f.Name.Contains("Angular")) ?? existingFolders[0];

            existingProjects = await projectsDb.Projects.Where(p => p.TenantId == tenantId).ToListAsync(cancellationToken);
            if (existingProjects.Count == 0)
            {
                var p1 = Project.Create(tenantId, spaceCore.Id, folderBackend.Id, "CRM SaaS Suite v2.0", "Migración a arquitectura limpia C# .NET 9 con MediatR y CQRS", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)), adminUser.Id);
                var p2 = Project.Create(tenantId, spaceCore.Id, folderFrontend.Id, "Rediseño ClickUp UI/UX", "Implementación de interfaz moderna con Tailwind CSS y componentes ShadCN", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), adminUser.Id);
                var p3 = Project.Create(tenantId, spaceCore.Id, folderBackend.Id, "Sistema de Webhooks Globals", "Infraestructura de suscripción a eventos con seguridad HMAC-SHA256", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21)), adminUser.Id);
                var p4 = Project.Create(tenantId, spaceOps.Id, null, "Portal de Clientes Enterprise", "Plataforma self-service para clientes corporativos con tableros interactivos", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)), adminUser.Id);
                var p5 = Project.Create(tenantId, spaceCore.Id, null, "Auditoría de Seguridad & Permisos", "Matriz de permisos granulares por usuario, equipo y rol", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), adminUser.Id);

                projectsDb.Projects.AddRange(p1, p2, p3, p4, p5);
                await projectsDb.SaveChangesAsync(cancellationToken);
                existingProjects = await projectsDb.Projects.Where(p => p.TenantId == tenantId).ToListAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding Projects module data");
        }

        // ---------------------------------------------------------------------
        // 4. TASKS / WORKITEMS
        // ---------------------------------------------------------------------
        try
        {
            var workItemsDb = scope.ServiceProvider.GetRequiredService<WorkItemsDbContext>();
            try { await workItemsDb.Database.ExecuteSqlAsync($"UPDATE `Tasks` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

            var existingTasks = await workItemsDb.Tasks.Where(t => t.TenantId == tenantId).ToListAsync(cancellationToken);

            if (existingTasks.Count < 10 && existingProjects.Count > 0)
            {
                var sampleTasks = new (string title, string desc, string status, decimal hours)[]
                {
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
                };

                int projIdx = 0;
                foreach (var tData in sampleTasks)
                {
                    var targetProject = existingProjects[projIdx % existingProjects.Count];
                    projIdx++;

                    var assignee = allUsers.Count > 0 ? allUsers[projIdx % allUsers.Count].Id : adminUser.Id;
                    var reporter = adminUser.Id;

                    var task = WorkTask.Create(
                        tenantId,
                        targetProject.Id,
                        tData.title,
                        tData.desc,
                        assignee,
                        reporter,
                        tData.hours,
                        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7 + projIdx)));

                    if (tData.status != "To Do")
                    {
                        task.Move("In Progress");
                        if (tData.status == "In Review") task.Move("In Review");
                        else if (tData.status == "Done") task.Move("Done");
                    }

                    workItemsDb.Tasks.Add(task);
                }
                await workItemsDb.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding WorkItems module data");
        }

        // ---------------------------------------------------------------------
        // 5. DOCS & TEMPLATES
        // ---------------------------------------------------------------------
        try
        {
            var docsDb = scope.ServiceProvider.GetRequiredService<DocsDbContext>();
            try
            {
                await docsDb.Database.ExecuteSqlAsync($"UPDATE `Documents` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
            }
            catch { }

            var existingDocs = await docsDb.Documents.Where(d => d.TenantId == tenantId).ToListAsync(cancellationToken);

            if (existingDocs.Count == 0)
            {
                var doc1 = Document.Create(tenantId, "Arquitectura del Sistema CRM SaaS Suite", "Especificaciones técnicas y guía de desarrollo", DocumentType.Wiki, adminUser.Id, null, null);
                var page1 = Page.Create(doc1.Id, null, "Visión General y Estructura", 
                    "<h1>Arquitectura CRM SaaS Suite</h1><p>El sistema está estructurado mediante <strong>Clean Architecture</strong>, <strong>DDD</strong> y <strong>CQRS</strong> con .NET 9 y Angular 19.</p><h2>Principios Clave</h2><ul><li>Modulo Identity con Refresh Tokens</li><li>ClickUp-Style Admin & Permisos Granulares</li><li>Integración de Webhooks con firmado HMAC</li></ul>", 1);
                doc1.AddPage(page1);

                var doc2 = Document.Create(tenantId, "Plantilla: Especificación de Producto (PRD)", "Plantilla predefinida para nuevos requerimientos", DocumentType.Template, adminUser.Id, null, null);
                var page2 = Page.Create(doc2.Id, null, "Estructura del PRD",
                    "<h1>Especificación del Producto</h1><h2>1. Objetivo del Negocio</h2><p>Describe el problema a resolver.</p><h2>2. Historias de Usuario</h2><p>Como usuario quiero X para Y.</p><h2>3. Criterios de Aceptación</h2><ul><li>Requerimiento 1</li><li>Requerimiento 2</li></ul>", 1);
                doc2.AddPage(page2);

                var doc3 = Document.Create(tenantId, "Minuta de Reunión: Planificación Sprint Q3", "Acuerdos y distribución de tareas", DocumentType.MeetingNote, adminUser.Id, null, null);
                var page3 = Page.Create(doc3.Id, null, "Acuerdos del Equipo",
                    "<h1>Minuta de Reunión - Q3</h1><p><strong>Asistentes:</strong> Admin, Sofia Arismendi, Carlos Mendoza.</p><h2>Acuerdos</h2><ol><li>Finalizar módulo de permisos granulares esta semana.</li><li>Revisar suscripciones de webhooks en Docker.</li></ol>", 1);
                doc3.AddPage(page3);

                docsDb.Documents.AddRange(doc1, doc2, doc3);
                await docsDb.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding Docs module data");
        }

        // ---------------------------------------------------------------------
        // 6. TICKETING / SUPPORT
        // ---------------------------------------------------------------------
        try
        {
            var ticketsDb = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
            try { await ticketsDb.Database.ExecuteSqlAsync($"UPDATE `Tickets` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

            var existingTickets = await ticketsDb.Tickets.Where(t => t.TenantId == tenantId).ToListAsync(cancellationToken);

            if (existingTickets.Count < 5)
            {
                var sampleTickets = new (string title, string desc, TicketPriority priority)[]
                {
                    ("Error 500 al consultar permisos por rol en el módulo Admin", "Al consultar /api/v1/permissions?targetType=Role se genera una excepción de columna no encontrada en MySQL.", TicketPriority.High),
                    ("Solicitud de integración de Webhooks con canal de Slack", "Requerimos enviar alertas automáticas cuando un ticket pase a estado Resuelto.", TicketPriority.Medium),
                    ("Duda sobre exportación de reportes de tareas a formato PDF", "¿Existe opción para descargar el reporte de rendimiento en PDF o Excel?", TicketPriority.Low),
                    ("Problema al subir imagen de perfil/avatar en configuración", "El sistema muestra error de tipo de archivo al intentar subir una imagen PNG.", TicketPriority.Medium),
                    ("Consulta sobre límite de miembros por equipo", "Necesitamos agregar 15 usuarios a un solo equipo de desarrollo.", TicketPriority.Low)
                };

                foreach (var tk in sampleTickets)
                {
                    var customerId = memberUsers.Count > 0 ? memberUsers[Random.Shared.Next(memberUsers.Count)].Id : adminUser.Id;
                    var res = Ticket.Create(tenantId, customerId, tk.title, tk.desc, tk.priority);
                    if (res.IsSuccess && res.Value != null)
                    {
                        var ticket = res.Value;
                        ticket.AssignTo(adminUser.Id);
                        ticketsDb.Tickets.Add(ticket);
                    }
                }
                await ticketsDb.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding Ticketing module data");
        }

        // ---------------------------------------------------------------------
        // 7. CALENDAR EVENTS
        // ---------------------------------------------------------------------
        try
        {
            var calendarDb = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
            try { await calendarDb.Database.ExecuteSqlAsync($"UPDATE `calendar_events` SET `tenant_id` = {tenantId} WHERE `tenant_id` != {tenantId}", cancellationToken); } catch { }

            var existingEvents = await calendarDb.CalendarEvents.Where(e => e.TenantId == tenantId).ToListAsync(cancellationToken);

            if (existingEvents.Count == 0)
            {
                var now = DateTime.UtcNow;
                var res1 = CalendarEvent.Create(tenantId, adminUser.Id, "Sprint Planning - CRM SaaS v2.0", now.AddDays(1).Date.AddHours(9), now.AddDays(1).Date.AddHours(10).AddMinutes(30), CalendarEventType.Meeting, null, null, "Planificación de tareas del sprint con todo el equipo de desarrollo", "Sala Virtual Meet", false);
                var res2 = CalendarEvent.Create(tenantId, adminUser.Id, "Demo de Producto con Cliente VIP - Acme Corp", now.AddDays(2).Date.AddHours(14), now.AddDays(2).Date.AddHours(15), CalendarEventType.Appointment, null, null, "Presentación de la nueva interfaz ClickUp y permisos granulares", "Google Meet Link", false);
                var res3 = CalendarEvent.Create(tenantId, adminUser.Id, "Revisión de Arquitectura & Webhooks", now.AddDays(3).Date.AddHours(11), now.AddDays(3).Date.AddHours(12), CalendarEventType.Meeting, null, null, "Auditoría de seguridad y firmado HMAC-SHA256 de webhooks", "Sala de Reuniones A", false);
                var res4 = CalendarEvent.Create(tenantId, adminUser.Id, "Despliegue a Producción v2.1", now.AddDays(5).Date.AddHours(8), now.AddDays(5).Date.AddHours(18), CalendarEventType.Task, null, null, "Despliegue de contenedores Docker y actualización de base de datos MySQL", "Servidor Cloud", true);

                if (res1.IsSuccess && res1.Value != null) calendarDb.CalendarEvents.Add(res1.Value);
                if (res2.IsSuccess && res2.Value != null) calendarDb.CalendarEvents.Add(res2.Value);
                if (res3.IsSuccess && res3.Value != null) calendarDb.CalendarEvents.Add(res3.Value);
                if (res4.IsSuccess && res4.Value != null) calendarDb.CalendarEvents.Add(res4.Value);

                await calendarDb.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding Calendar module data");
        }

        // ---------------------------------------------------------------------
        // 8. COMMUNICATION & CHAT
        // ---------------------------------------------------------------------
        try
        {
            var commDb = scope.ServiceProvider.GetRequiredService<CommunicationsDbContext>();
            try
            {
                await commDb.Database.ExecuteSqlAsync($"UPDATE `Conversations` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
                await commDb.Database.ExecuteSqlAsync($"UPDATE `Messages` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
            }
            catch { }

            var existingConvs = await commDb.Conversations.Where(c => c.TenantId == tenantId).ToListAsync(cancellationToken);

            if (existingConvs.Count == 0)
            {
                var resC1 = Conversation.Create(tenantId, "#general", ConversationType.Channel);
                var resC2 = Conversation.Create(tenantId, "#desarrollo-backend", ConversationType.Channel);
                var resC3 = Conversation.Create(tenantId, "#frontend-angular", ConversationType.Channel);

                if (resC1.IsSuccess && resC1.Value != null && resC2.IsSuccess && resC2.Value != null && resC3.IsSuccess && resC3.Value != null)
                {
                    var c1 = resC1.Value;
                    var c2 = resC2.Value;
                    var c3 = resC3.Value;

                    commDb.Conversations.AddRange(c1, c2, c3);
                    await commDb.SaveChangesAsync(cancellationToken);

                    var resM1 = Message.Create(tenantId, c1.Id, adminUser.Id, "¡Hola a todos! Bienvenidos al espacio oficial de CRM SaaS Suite.");
                    var resM2 = Message.Create(tenantId, c2.Id, adminUser.Id, "Completamos la migración a .NET 9 con MediatR y CQRS. Los handlers están probados.");
                    var resM3 = Message.Create(tenantId, c3.Id, adminUser.Id, "La interfaz del Centro de Admin estilo ClickUp ya está lista y funcionando.");

                    if (resM1.IsSuccess && resM1.Value != null) commDb.Messages.Add(resM1.Value);
                    if (resM2.IsSuccess && resM2.Value != null) commDb.Messages.Add(resM2.Value);
                    if (resM3.IsSuccess && resM3.Value != null) commDb.Messages.Add(resM3.Value);

                    if (memberUsers.Count > 0)
                    {
                        var resM4 = Message.Create(tenantId, c1.Id, memberUsers[0].Id, "¡Excelente noticia! Ya estoy probando las funciones con la data de demostración.");
                        if (resM4.IsSuccess && resM4.Value != null) commDb.Messages.Add(resM4.Value);
                    }

                    await commDb.SaveChangesAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding Communication module data");
        }

        // ---------------------------------------------------------------------
        // 9. NOTIFICATIONS
        // ---------------------------------------------------------------------
        try
        {
            var notifDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            try { await notifDb.Database.ExecuteSqlAsync($"UPDATE `Notifications` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

            var existingNotifs = await notifDb.Notifications.Where(n => n.TenantId == tenantId && n.RecipientUserId == adminUser.Id).ToListAsync(cancellationToken);

            if (existingNotifs.Count == 0)
            {
                var resN1 = Notification.Create(tenantId, adminUser.Id, NotificationType.InApp.Name, "Tarea Asignada", "Te han asignado la tarea: Implementar Webhooks v2");
                var resN2 = Notification.Create(tenantId, adminUser.Id, NotificationType.InApp.Name, "Nuevo Ticket Prioritario", "Se ha registrado un ticket sobre la consulta de permisos por rol.");
                var resN3 = Notification.Create(tenantId, adminUser.Id, NotificationType.InApp.Name, "Mención en Documento", "Sofia te ha mencionado en la especificación técnica de arquitectura.");

                if (resN1.IsSuccess && resN1.Value != null)
                {
                    resN1.Value.MarkAsRead();
                    notifDb.Notifications.Add(resN1.Value);
                }
                if (resN2.IsSuccess && resN2.Value != null) notifDb.Notifications.Add(resN2.Value);
                if (resN3.IsSuccess && resN3.Value != null) notifDb.Notifications.Add(resN3.Value);

                await notifDb.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding Notifications module data");
        }

        // ---------------------------------------------------------------------
        // 10. WEBHOOKS
        // ---------------------------------------------------------------------
        try
        {
            var webhookDb = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
            try { await webhookDb.Database.ExecuteSqlAsync($"UPDATE `webhook_subscriptions` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

            var existingWebhooks = await webhookDb.Subscriptions.Where(w => w.TenantId == tenantId).ToListAsync(cancellationToken);

            if (existingWebhooks.Count == 0)
            {
                var w1 = WebhookSubscription.Create(tenantId, "task.created", "https://hooks.slack.com/services/T00/B00/X00", "whsec_slack_123456789");
                var w2 = WebhookSubscription.Create(tenantId, "ticket.updated", "https://hooks.zapier.com/hooks/catch/12345/abcde", "whsec_zapier_987654321");
                var w3 = WebhookSubscription.Create(tenantId, "user.created", "https://http-intake.logs.datadoghq.com/v1/input", "whsec_datadog_456789123");

                webhookDb.Subscriptions.AddRange(w1, w2, w3);
                await webhookDb.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding Webhook module data");
        }

        // ---------------------------------------------------------------------
        // 11. TAGS
        // ---------------------------------------------------------------------
        try
        {
            var tagsDb = scope.ServiceProvider.GetRequiredService<TagsDbContext>();
            try { await tagsDb.Database.ExecuteSqlAsync($"UPDATE `Tags` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

            var existingTags = await tagsDb.Tags.Where(t => t.TenantId == tenantId).ToListAsync(cancellationToken);

            if (existingTags.Count == 0)
            {
                var tag1 = Tag.Create(tenantId, "🔥 Crítico", "#EF4444", "Priority");
                var tag2 = Tag.Create(tenantId, "⚡ Backend C#", "#8B5CF6", "Tech");
                var tag3 = Tag.Create(tenantId, "🎨 Frontend Angular", "#3B82F6", "Tech");
                var tag4 = Tag.Create(tenantId, "🔒 Seguridad", "#10B981", "Security");
                var tag5 = Tag.Create(tenantId, "⭐ VIP Client", "#F59E0B", "Business");
                var tag6 = Tag.Create(tenantId, "🚀 Q3 Release", "#EC4899", "Milestone");

                tagsDb.Tags.AddRange(tag1, tag2, tag3, tag4, tag5, tag6);
                await tagsDb.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding Tags module data");
        }

        logger.LogInformation("Global data seeding completed successfully across all modules!");
    }
}
