using Docs.Domain.Entities;
using Docs.Domain.ValueObjects;
using Docs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Seeding;

public sealed class DocsSeeder(DocsDbContext docsDb) : IModuleSeeder
{
    public string Module => "Docs";
    public int Order => 50;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var admin = context.Admin;

        using var _ = docsDb.AsTenant(tenantId);
        try { await docsDb.Database.ExecuteSqlAsync($"UPDATE `Documents` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

        if (await docsDb.Documents.AnyAsync(d => d.TenantId == tenantId, cancellationToken))
            return;

        var architecture = Document.Create(tenantId, "Arquitectura del Sistema CRM SaaS Suite", "Especificaciones técnicas y guía de desarrollo", DocumentType.Wiki, admin.Id, null, null);
        architecture.AddPage(Page.Create(architecture.Id, null, "Visión General y Estructura",
            "<h1>Arquitectura CRM SaaS Suite</h1><p>El sistema está estructurado mediante <strong>Clean Architecture</strong>, <strong>DDD</strong> y <strong>CQRS</strong> con .NET 9 y Angular 19.</p><h2>Principios Clave</h2><ul><li>Modulo Identity con Refresh Tokens</li><li>ClickUp-Style Admin & Permisos Granulares</li><li>Integración de Webhooks con firmado HMAC</li></ul>", 1));

        var productSpec = Document.Create(tenantId, "Plantilla: Especificación de Producto (PRD)", "Plantilla predefinida para nuevos requerimientos", DocumentType.Template, admin.Id, null, null);
        productSpec.AddPage(Page.Create(productSpec.Id, null, "Estructura del PRD",
            "<h1>Especificación del Producto</h1><h2>1. Objetivo del Negocio</h2><p>Describe el problema a resolver.</p><h2>2. Historias de Usuario</h2><p>Como usuario quiero X para Y.</p><h2>3. Criterios de Aceptación</h2><ul><li>Requerimiento 1</li><li>Requerimiento 2</li></ul>", 1));

        var meetingNotes = Document.Create(tenantId, "Minuta de Reunión: Planificación Sprint Q3", "Acuerdos y distribución de tareas", DocumentType.MeetingNote, admin.Id, null, null);
        meetingNotes.AddPage(Page.Create(meetingNotes.Id, null, "Acuerdos del Equipo",
            "<h1>Minuta de Reunión - Q3</h1><p><strong>Asistentes:</strong> Admin, Sofia Arismendi, Carlos Mendoza.</p><h2>Acuerdos</h2><ol><li>Finalizar módulo de permisos granulares esta semana.</li><li>Revisar suscripciones de webhooks en Docker.</li></ol>", 1));

        docsDb.Documents.AddRange(architecture, productSpec, meetingNotes);
        await docsDb.SaveChangesAsync(cancellationToken);
    }
}
