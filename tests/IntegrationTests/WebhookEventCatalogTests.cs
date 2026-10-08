using System.Reflection;
using System.Runtime.CompilerServices;
using BuildingBlocks.Application.Abstractions;
using FluentAssertions;
using Webhook.Domain;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El catálogo de eventos de webhook y los comandos que los emiten dicen lo mismo.
///
/// Antes no: el catálogo tenía «report.generated», que no emitía nadie, y los comandos emitían
/// «workitem.checklist.added» y otros que el catálogo no tenía. Una suscripción podía elegir un
/// evento que no llegaba nunca, y un evento podía salir sin que nadie pudiera suscribirse.
///
/// Está en las pruebas de integración y no en las unitarias porque necesita ver todos los módulos,
/// y es este proyecto el que los tiene todos a través de la API.
/// </summary>
public sealed class WebhookEventCatalogTests
{
    /// <summary>El nombre que emite cada comando, leído sin construirlo: es una propiedad fija.</summary>
    private static IReadOnlyDictionary<string, string> EmittedEvents()
    {
        var seen = new HashSet<string>();
        var pending = new Queue<Assembly>([typeof(Program).Assembly]);
        var emitted = new Dictionary<string, string>();

        while (pending.Count > 0)
        {
            var assembly = pending.Dequeue();
            if (!seen.Add(assembly.FullName!)) continue;

            foreach (var reference in assembly.GetReferencedAssemblies().Where(r => r.Name is not null
                         && (r.Name.StartsWith("BuildingBlocks") || r.Name.Contains(".Application") || r.Name.Contains(".Presentation")
                             || r.Name.Contains(".Infrastructure") || r.Name.Contains(".Domain"))))
                pending.Enqueue(Assembly.Load(reference));

            foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                                                               && typeof(IWebhookTriggered).IsAssignableFrom(t)))
            {
                var instance = (IWebhookTriggered)RuntimeHelpers.GetUninitializedObject(type);
                emitted[type.FullName!] = instance.WebhookEventName;
            }
        }

        return emitted;
    }

    [Fact]
    public void Every_event_a_command_emits_can_be_subscribed()
    {
        var notInCatalog = EmittedEvents().Where(e => !WebhookEventCatalog.Exists(e.Value)).Select(e => $"{e.Key} → {e.Value}");

        notInCatalog.Should().BeEmpty("un evento que sale sin estar en el catálogo no lo puede pedir nadie");
    }

    [Fact]
    public void Every_event_in_the_catalog_is_emitted_by_some_command()
    {
        var emitted = EmittedEvents().Values.ToHashSet();

        WebhookEventCatalog.All.Select(e => e.Name).Where(n => !emitted.Contains(n))
            .Should().BeEmpty("una suscripción a un evento que nadie emite no recibiría nada nunca, y no lo diría");
    }
}
