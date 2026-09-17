using Communication.Domain.Entities;
using Communication.Domain.ValueObjects;
using Communication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Seeding;

public sealed class CommunicationSeeder(CommunicationsDbContext communicationDb) : IModuleSeeder
{
    public string Module => "Communication";
    public int Order => 80;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var admin = context.Admin;

        using var _ = communicationDb.AsTenant(tenantId);
        try
        {
            await communicationDb.Database.ExecuteSqlAsync($"UPDATE `Conversations` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
            await communicationDb.Database.ExecuteSqlAsync($"UPDATE `Messages` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
        }
        catch { }

        if (await communicationDb.Conversations.AnyAsync(c => c.TenantId == tenantId, cancellationToken))
            return;

        var general = Conversation.Create(tenantId, "#general", ConversationType.Channel);
        var backend = Conversation.Create(tenantId, "#desarrollo-backend", ConversationType.Channel);
        var frontend = Conversation.Create(tenantId, "#frontend-angular", ConversationType.Channel);

        if (general.Value is null || backend.Value is null || frontend.Value is null
            || general.IsFailure || backend.IsFailure || frontend.IsFailure)
            return;

        communicationDb.Conversations.AddRange(general.Value, backend.Value, frontend.Value);
        await communicationDb.SaveChangesAsync(cancellationToken);

        var messages = new List<BuildingBlocks.Domain.Result<Message>>
        {
            Message.Create(tenantId, general.Value.Id, admin.Id, "¡Hola a todos! Bienvenidos al espacio oficial de CRM SaaS Suite."),
            Message.Create(tenantId, backend.Value.Id, admin.Id, "Completamos la migración a .NET 9 con MediatR y CQRS. Los handlers están probados."),
            Message.Create(tenantId, frontend.Value.Id, admin.Id, "La interfaz del Centro de Admin estilo ClickUp ya está lista y funcionando.")
        };

        if (context.Members.Count > 0)
            messages.Add(Message.Create(tenantId, general.Value.Id, context.Members[0].Id, "¡Excelente noticia! Ya estoy probando las funciones con la data de demostración."));

        foreach (var created in messages)
        {
            if (created.IsSuccess && created.Value != null)
                communicationDb.Messages.Add(created.Value);
        }

        await communicationDb.SaveChangesAsync(cancellationToken);
    }
}
