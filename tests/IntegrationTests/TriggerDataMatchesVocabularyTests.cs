using ApiHost.Services;
using Automations.Application.Abstractions;
using Automations.Domain.ValueObjects;
using BuildingBlocks.Application.Events;
using FluentAssertions;
using WorkItems.Domain.Events;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Quien dispara las automatizaciones rellena exactamente los campos que el vocabulario declara
/// para cada disparador.
///
/// Es lo que impide que vuelvan a separarse. Si el puente deja de mandar un campo declarado, las
/// condiciones sobre él dejan de cumplirse sin ningún error; si manda uno no declarado, la
/// interfaz no deja usarlo y el dato se pierde. Las dos cosas ya pasaron con «se crea una tarea»
/// y el título.
///
/// No necesita la API ni la base de datos: llama a los traductores directamente.
/// </summary>
public sealed class TriggerDataMatchesVocabularyTests
{
    private sealed class RecordingEngine : IAutomationEngine
    {
        public AutomationTriggerEvent? Last { get; private set; }

        public Task<int> RunAsync(AutomationTriggerEvent triggerEvent, CancellationToken ct = default)
        {
            Last = triggerEvent;
            return Task.FromResult(0);
        }
    }

    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();

    private static async Task<AutomationTriggerEvent> TranslateAsync<TEvent>(TEvent domainEvent)
        where TEvent : BuildingBlocks.Domain.Primitives.DomainEvent
    {
        var engine = new RecordingEngine();
        var bridge = new AutomationsBridge(engine);

        await (domainEvent switch
        {
            TaskCreatedEvent e => bridge.Handle(new DomainEventNotification<TaskCreatedEvent>(e), default),
            TaskStatusChangedEvent e => bridge.Handle(new DomainEventNotification<TaskStatusChangedEvent>(e), default),
            TaskPriorityChangedEvent e => bridge.Handle(new DomainEventNotification<TaskPriorityChangedEvent>(e), default),
            _ => throw new ArgumentOutOfRangeException(nameof(domainEvent)),
        });

        return engine.Last ?? throw new InvalidOperationException("El puente no disparó nada");
    }

    [Fact]
    public async Task TaskCreated_carries_exactly_the_declared_fields()
    {
        var fired = await TranslateAsync(new TaskCreatedEvent(TaskId, Tenant, Project, Guid.NewGuid(), "Revisar el 8b", "To Do", "Normal"));

        fired.Trigger.Should().Be(TriggerTypes.TaskCreated);
        fired.Data.Keys.Should().BeEquivalentTo(EventFields.ForTrigger(TriggerTypes.TaskCreated));
        fired.Data[EventFields.Title].Should().Be("Revisar el 8b");
    }

    [Fact]
    public async Task TaskStatusChanged_carries_exactly_the_declared_fields()
    {
        var fired = await TranslateAsync(new TaskStatusChangedEvent(TaskId, Tenant, Project, "To Do", "Done", "Revisar el 8b", "Normal", Guid.NewGuid()));

        fired.Trigger.Should().Be(TriggerTypes.TaskStatusChanged);
        fired.Data.Keys.Should().BeEquivalentTo(EventFields.ForTrigger(TriggerTypes.TaskStatusChanged));
    }

    [Fact]
    public async Task TaskPriorityChanged_carries_exactly_the_declared_fields()
    {
        var fired = await TranslateAsync(new TaskPriorityChangedEvent(TaskId, Tenant, Project, "Normal", "High", "Revisar el 8b", "To Do", Guid.NewGuid()));

        fired.Trigger.Should().Be(TriggerTypes.TaskPriorityChanged);
        fired.Data.Keys.Should().BeEquivalentTo(EventFields.ForTrigger(TriggerTypes.TaskPriorityChanged));
    }

    [Fact]
    public void TaskDueSoon_carries_exactly_the_declared_fields()
    {
        var today = new DateOnly(2026, 10, 1);

        var data = DueDateWatcher.TriggerData(
            today, today.AddDays(2), "To Do", "High", Project, Guid.NewGuid(), "Revisar el 8b");

        data.Keys.Should().BeEquivalentTo(EventFields.ForTrigger(TriggerTypes.TaskDueSoon));
        data[EventFields.DaysUntilDue].Should().Be("2");
    }

    /// <summary>Si se añade un disparador, también tiene que tener su prueba aquí.</summary>
    [Fact]
    public void Every_trigger_is_covered_here()
    {
        TriggerTypes.All().Should().BeEquivalentTo(
        [
            TriggerTypes.TaskCreated, TriggerTypes.TaskStatusChanged,
            TriggerTypes.TaskPriorityChanged, TriggerTypes.TaskDueSoon,
        ]);
    }
}
