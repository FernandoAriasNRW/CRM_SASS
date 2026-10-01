using FluentAssertions;
using WorkItems.Domain.Entities;
using WorkItems.Domain.Events;
using WorkItems.Domain.ValueObjects;
using Xunit;

namespace UnitTests;

/// <summary>
/// Invariantes de WorkTask. Se prueban contra el agregado directamente, sin base de
/// datos: la máquina de estados es una regla de negocio y debe sostenerse por sí sola,
/// aunque cambie la persistencia.
/// </summary>
public sealed class WorkTaskInvariantsTests
{
    private static WorkTask NewTask() => WorkTask.Create(
        tenantId: Guid.NewGuid(),
        projectId: Guid.NewGuid(),
        title: "Tarea de prueba",
        description: "descripción",
        assigneeId: Guid.NewGuid(),
        createdById: Guid.NewGuid(),
        estimatedHours: 8m,
        dueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

    [Fact]
    public void A_task_starts_in_To_Do_and_raises_a_creation_event()
    {
        var task = NewTask();

        task.Status.Value.ToString().Should().Be("To Do");
        task.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TaskCreatedEvent>();
    }

    /// <summary>
    /// Cualquier estado existente es alcanzable desde cualquier otro.
    ///
    /// El dominio tuvo una máquina de estados que restringía las transiciones y se
    /// retiró a propósito: qué movimiento tiene sentido lo decide quien gestiona el
    /// trabajo, y las reglas estorbaban en casos reales —reabrir algo dado por hecho,
    /// mandar a espera algo que ni se empezó— sin evitar ningún dato incorrecto.
    ///
    /// Se recorren todas las combinaciones en lugar de una muestra: si alguien reintroduce
    /// una restricción, este test la detecta sea cual sea.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Any_status_is_reachable_from_any_other(string from, string to)
    {
        var task = NewTask();
        task.Move(from);

        task.Move(to);

        task.Status.Value.ToString().Should().Be(to);
    }

    public static TheoryData<string, string> AllCombinations()
    {
        var statuses = new[] { "To Do", "In Progress", "In Review", "Done", "On Hold" };
        var data = new TheoryData<string, string>();
        foreach (var from in statuses)
            foreach (var to in statuses)
                data.Add(from, to);
        return data;
    }

    [Fact]
    public void An_unknown_status_is_not_accepted()
    {
        // Lo único que sigue rechazándose. No es política de flujo: un estado que no
        // existe es un dato corrupto, y aceptarlo dejaría la tarea en un limbo que
        // ninguna vista sabría representar.
        var task = NewTask();

        var move = () => task.Move("Archivada");

        move.Should().Throw<InvalidOperationException>()
            .WithMessage("*no existe*");
    }

    [Fact]
    public void Moving_raises_the_event_with_old_and_new_status()
    {
        var task = NewTask();
        task.ClearDomainEvents();

        task.Move("In Progress");

        var domainEvent = task.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskStatusChangedEvent>().Subject;
        domainEvent.OldStatus.Should().Be("To Do");
        domainEvent.NewStatus.Should().Be("In Progress");
    }

    [Fact]
    public void Reassigning_raises_an_assignment_event()
    {
        var task = NewTask();
        task.ClearDomainEvents();
        var newAssignee = Guid.NewGuid();

        task.Assign(newAssignee);

        task.AssigneeId.Should().Be(newAssignee);
        task.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TaskAssignedEvent>();
    }

    [Fact]
    public void Adding_the_same_tag_twice_does_not_duplicate_it()
    {
        var task = NewTask();
        var tag = Guid.NewGuid();

        task.AddTag(tag);
        task.AddTag(tag);

        task.TagIds.Should().ContainSingle().Which.Should().Be(tag);
    }

    [Fact]
    public void Removing_a_missing_tag_does_not_fail()
    {
        var task = NewTask();

        var remove = () => task.RemoveTag(Guid.NewGuid());

        remove.Should().NotThrow();
        task.TagIds.Should().BeEmpty();
    }

    #region Prioridad

    [Fact]
    public void A_task_without_explicit_priority_starts_Normal()
    {
        var task = NewTask();

        task.Priority.Value.Should().Be("Normal");
    }

    [Fact]
    public void A_task_can_start_with_the_given_priority()
    {
        var task = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Urgente", "descripción",
            Guid.NewGuid(), Guid.NewGuid(), 4m,
            DateOnly.FromDateTime(DateTime.UtcNow), "Urgent");

        task.Priority.Value.Should().Be("Urgent");
    }

    /// <summary>
    /// Igual que con el estado, cualquier prioridad existente es alcanzable desde cualquier
    /// otra, y en los dos sentidos. Subir o bajar una tarea es de quien gestiona el trabajo:
    /// si alguien introduce una regla de «no se puede bajar de Urgente», este test la caza.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllPriorityCombinations))]
    public void Any_priority_is_reachable_from_any_other(string from, string to)
    {
        var task = NewTask();
        task.Reprioritize(from);

        task.Reprioritize(to);

        task.Priority.Value.Should().Be(to);
    }

    public static TheoryData<string, string> AllPriorityCombinations()
    {
        var priorities = TaskPriority.All().Select(p => p.Value).ToArray();
        var data = new TheoryData<string, string>();
        foreach (var from in priorities)
            foreach (var to in priorities)
                data.Add(from, to);
        return data;
    }

    [Fact]
    public void An_unknown_priority_is_not_accepted_on_reprioritize()
    {
        var task = NewTask();

        var repriorizar = () => task.Reprioritize("Crítica");

        repriorizar.Should().Throw<InvalidOperationException>().WithMessage("*no existe*");
        task.Priority.Value.Should().Be("Normal", "una prioridad inválida no debe dejar la tarea a medias");
    }

    [Fact]
    public void An_unknown_priority_is_not_accepted_on_create()
    {
        var create = () => WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Tarea", "descripción",
            Guid.NewGuid(), Guid.NewGuid(), 1m,
            DateOnly.FromDateTime(DateTime.UtcNow), "Altísima");

        create.Should().Throw<InvalidOperationException>().WithMessage("*no existe*");
    }

    [Fact]
    public void Reprioritizing_raises_the_event_with_old_and_new()
    {
        var task = NewTask();
        task.ClearDomainEvents();

        task.Reprioritize("Urgent");

        var domainEvent = task.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskPriorityChangedEvent>().Subject;
        domainEvent.OldPriority.Should().Be("Normal");
        domainEvent.NewPriority.Should().Be("Urgent");
    }

    [Fact]
    public void Reprioritizing_to_the_same_priority_raises_no_event()
    {
        // Sin cambio real no hay nada que contar. Un evento vacío haría trabajar de más a
        // las automatizaciones que se apoyarán en él.
        var task = NewTask();
        task.ClearDomainEvents();

        task.Reprioritize("Normal");

        task.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void The_priority_keeps_its_stored_name()
    {
        // TaskStatus construye el estado desde su valor al mover, y deja el nombre igual que
        // el valor: una tarea movida a «Done» acaba con nombre «Done» en lugar de
        // «Completado». La prioridad usa la instancia canónica para no repetirlo.
        var task = NewTask();

        task.Reprioritize("Urgent");

        task.Priority.Name.Should().Be("Urgente");
    }

    /// <summary>
    /// El orden es el de negocio, no el alfabético.
    ///
    /// Alfabéticamente sería High, Low, Normal, Urgent, que no significa nada. Este test fija
    /// el orden porque de él depende la ordenación de listas y tableros: el rango que usa la
    /// consulta se construye a partir de <c>TaskPriority.All()</c>, así que si alguien
    /// reordena o añade una prioridad, aquí se ve.
    /// </summary>
    [Fact]
    public void Priority_order_is_a_business_order()
    {
        TaskPriority.All().Select(p => p.Value)
            .Should().ContainInOrder("Urgent", "High", "Normal", "Low")
            .And.HaveCount(4);

        TaskPriority.OrderOf("Urgent").Should().BeLessThan(TaskPriority.OrderOf("Low"));
    }

    [Fact]
    public void An_unknown_priority_sorts_last()
    {
        // Cubre las filas antiguas que pudieran tener la columna vacía: deben caer al fondo,
        // no colarse en la cabecera como si fueran lo más urgente.
        TaskPriority.OrderOf("").Should().BeGreaterThan(TaskPriority.OrderOf("Low"));
    }

    #endregion

    #region Subtareas

    [Fact]
    public void A_task_starts_top_level()
    {
        var task = NewTask();

        task.ParentTaskId.Should().BeNull();
        task.IsSubtask.Should().BeFalse();
    }

    [Fact]
    public void A_task_can_start_as_a_subtask()
    {
        var parent = Guid.NewGuid();

        var task = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Subtarea", "descripción",
            Guid.NewGuid(), Guid.NewGuid(), 1m,
            DateOnly.FromDateTime(DateTime.UtcNow), null, parent);

        task.ParentTaskId.Should().Be(parent);
        task.IsSubtask.Should().BeTrue();
    }

    [Fact]
    public void A_task_cannot_be_its_own_subtask()
    {
        // La única de las tres reglas de anidamiento que el agregado puede comprobar solo.
        var task = NewTask();

        var reparent = () => task.Reparent(task.Id);

        reparent.Should().Throw<InvalidOperationException>().WithMessage("*de sí misma*");
        task.ParentTaskId.Should().BeNull();
    }

    [Fact]
    public void Reparenting_raises_the_event_with_old_and_new_parent()
    {
        var task = NewTask();
        var firstParent = Guid.NewGuid();
        var secondParent = Guid.NewGuid();
        task.Reparent(firstParent);
        task.ClearDomainEvents();

        task.Reparent(secondParent);

        var domainEvent = task.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskParentChangedEvent>().Subject;
        domainEvent.OldParentTaskId.Should().Be(firstParent);
        domainEvent.NewParentTaskId.Should().Be(secondParent);
    }

    [Fact]
    public void Detaching_makes_the_task_top_level_and_says_so()
    {
        var task = NewTask();
        task.Reparent(Guid.NewGuid());
        task.ClearDomainEvents();

        task.Reparent(null);

        task.IsSubtask.Should().BeFalse();
        task.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskParentChangedEvent>()
            .Which.NewParentTaskId.Should().BeNull();
    }

    [Fact]
    public void Reparenting_to_the_current_parent_raises_no_event()
    {
        var task = NewTask();
        var parent = Guid.NewGuid();
        task.Reparent(parent);
        task.ClearDomainEvents();

        task.Reparent(parent);

        task.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void An_empty_parent_id_is_not_accepted()
    {
        // Guid.Empty no es «sin padre», es un dato mal formado: sin padre es null. Aceptarlo
        // dejaría una subtarea colgando de una tarea que no existe.
        var task = NewTask();

        var reparent = () => task.Reparent(Guid.Empty);

        reparent.Should().Throw<InvalidOperationException>().WithMessage("*no es válido*");
    }

    [Fact]
    public void Nesting_is_limited_to_one_level()
    {
        // Las reglas viven en un solo sitio para que el handler que las aplica no las
        // reinvente con otros mensajes.
        WorkTask.NestingRules.MaxDepth.Should().Be(2);
        WorkTask.NestingRules.ParentIsSubtask.Should().Contain("un solo nivel");
    }

    #endregion

    #region Responsables

    [Fact]
    public void A_task_created_with_an_assignee_also_has_it_in_the_collection()
    {
        // La invariante que sostiene todo lo demás: el principal siempre figura entre los
        // responsables. Si no, ninguna vista de las nuevas encontraría la tarea.
        var assignee = Guid.NewGuid();

        var task = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Con responsable", "x",
            assignee, Guid.NewGuid(), 1m, DateOnly.FromDateTime(DateTime.UtcNow));

        task.AssigneeId.Should().Be(assignee);
        task.Assignees.Select(a => a.UserId).Should().ContainSingle().Which.Should().Be(assignee);
        task.IsAssignee(assignee).Should().BeTrue();
    }

    [Fact]
    public void An_unassigned_task_has_no_assignees()
    {
        var task = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Sin asignar", "x",
            Guid.Empty, Guid.NewGuid(), 1m, DateOnly.FromDateTime(DateTime.UtcNow));

        task.AssigneeId.Should().Be(Guid.Empty);
        task.Assignees.Should().BeEmpty("el Guid vacío significa «sin asignar», no una persona");
    }

    [Fact]
    public void Adding_assignees_does_not_change_the_primary()
    {
        var task = NewTask();
        var principal = task.AssigneeId;
        var other = Guid.NewGuid();

        task.AddAssignee(other);

        task.AssigneeId.Should().Be(principal);
        task.Assignees.Select(a => a.UserId).Should().BeEquivalentTo([principal, other]);
    }

    [Fact]
    public void The_first_person_on_an_unassigned_task_becomes_primary()
    {
        // Lo contrario dejaría el campo del principal vacío con responsables dentro, que es
        // exactamente la incoherencia que la colección viene a evitar.
        var task = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Sin asignar", "x",
            Guid.Empty, Guid.NewGuid(), 1m, DateOnly.FromDateTime(DateTime.UtcNow));
        var someone = Guid.NewGuid();

        task.AddAssignee(someone);

        task.AssigneeId.Should().Be(someone);
    }

    [Fact]
    public void The_same_person_cannot_be_added_twice()
    {
        var task = NewTask();

        var repeat = () => task.AddAssignee(task.AssigneeId);

        repeat.Should().Throw<InvalidOperationException>().WithMessage("*ya es responsable*");
        task.Assignees.Should().HaveCount(1);
    }

    [Fact]
    public void Removing_the_primary_promotes_the_next()
    {
        // Sin promoción, la tarea quedaría con un principal que ya no es responsable.
        var task = NewTask();
        var principal = task.AssigneeId;
        var second = Guid.NewGuid();
        task.AddAssignee(second);

        task.RemoveAssignee(principal);

        task.AssigneeId.Should().Be(second);
        task.Assignees.Select(a => a.UserId).Should().ContainSingle().Which.Should().Be(second);
    }

    [Fact]
    public void Removing_the_last_assignee_leaves_the_task_unassigned()
    {
        var task = NewTask();

        task.RemoveAssignee(task.AssigneeId);

        task.AssigneeId.Should().Be(Guid.Empty);
        task.Assignees.Should().BeEmpty();
    }

    [Fact]
    public void Removing_a_non_assignee_is_rejected()
    {
        var task = NewTask();

        var remove = () => task.RemoveAssignee(Guid.NewGuid());

        remove.Should().Throw<InvalidOperationException>().WithMessage("*no es responsable*");
        task.Assignees.Should().HaveCount(1);
    }

    [Fact]
    public void Changing_the_primary_adds_it_to_the_collection_first()
    {
        var task = NewTask();
        var newValue = Guid.NewGuid();

        task.Assign(newValue);

        task.AssigneeId.Should().Be(newValue);
        task.IsAssignee(newValue).Should().BeTrue("el principal figura siempre entre los responsables");
        task.Assignees.Should().HaveCount(2, "el anterior sigue siendo responsable, sólo deja de ser el principal");
    }

    [Fact]
    public void Promoting_an_existing_assignee_does_not_duplicate_it()
    {
        var task = NewTask();
        var second = Guid.NewGuid();
        task.AddAssignee(second);

        task.Assign(second);

        task.Assignees.Select(a => a.UserId).Should().HaveCount(2).And.OnlyHaveUniqueItems();
        task.AssigneeId.Should().Be(second);
    }

    [Fact]
    public void Unassigning_completely_empties_the_collection()
    {
        var task = NewTask();
        task.AddAssignee(Guid.NewGuid());

        task.Assign(Guid.Empty);

        task.AssigneeId.Should().Be(Guid.Empty);
        task.Assignees.Should().BeEmpty("«sin asignar» no puede convivir con responsables dentro");
    }

    [Fact]
    public void Adding_and_removing_assignees_raises_their_events()
    {
        var task = NewTask();
        var someone = Guid.NewGuid();
        task.ClearDomainEvents();

        task.AddAssignee(someone);
        task.RemoveAssignee(someone);

        task.DomainEvents.Should().HaveCount(2);
        task.DomainEvents.First().Should().BeOfType<TaskAssigneeAddedEvent>();
        task.DomainEvents.Last().Should().BeOfType<TaskAssigneeRemovedEvent>();
    }

    #endregion

    #region Checklist

    [Fact]
    public void A_task_starts_without_checklist()
    {
        NewTask().Checklist.Should().BeEmpty();
    }

    [Fact]
    public void Items_are_appended_with_increasing_positions()
    {
        var task = NewTask();

        task.AddChecklistItem("Primero");
        task.AddChecklistItem("Segundo");
        task.AddChecklistItem("Tercero");

        task.Checklist.OrderBy(i => i.Position).Select(i => i.Text)
            .Should().ContainInOrder("Primero", "Segundo", "Tercero");
        task.Checklist.Select(i => i.Position).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Deleting_from_the_middle_does_not_tie_two_items_in_order()
    {
        // La posición se calcula sobre la mayor existente, no contando puntos: contarlos daría
        // una posición repetida en cuanto se borrara alguno del medio, y el orden dejaría de
        // estar definido.
        var task = NewTask();
        task.AddChecklistItem("Primero");
        var middle = task.AddChecklistItem("Segundo");
        task.AddChecklistItem("Tercero");

        task.RemoveChecklistItem(middle.Id);
        task.AddChecklistItem("Cuarto");

        task.Checklist.Select(i => i.Position).Should().OnlyHaveUniqueItems();
        task.Checklist.OrderBy(i => i.Position).Last().Text.Should().Be("Cuarto");
    }

    [Fact]
    public void An_item_without_text_is_not_accepted()
    {
        var task = NewTask();

        var empty = () => task.AddChecklistItem("   ");

        empty.Should().Throw<InvalidOperationException>().WithMessage("*necesita un texto*");
        task.Checklist.Should().BeEmpty();
    }

    [Fact]
    public void A_too_long_text_is_not_accepted()
    {
        var task = NewTask();

        var tooLong = () => task.AddChecklistItem(new string('x', ChecklistItem.MaxLength + 1));

        tooLong.Should().Throw<InvalidOperationException>().WithMessage("*no puede pasar de*");
    }

    [Fact]
    public void The_text_is_trimmed_when_saved()
    {
        var task = NewTask();

        var item = task.AddChecklistItem("  con espacios  ");

        item.Text.Should().Be("con espacios");
    }

    [Fact]
    public void Checking_an_item_counts_in_progress_and_raises_an_event()
    {
        var task = NewTask();
        var one = task.AddChecklistItem("Uno");
        task.AddChecklistItem("Dos");
        task.ClearDomainEvents();

        task.UpdateChecklistItem(one.Id, done: true, text: null);

        task.ChecklistProgress().Should().Be((2, 1));
        task.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskChecklistItemToggledEvent>()
            .Which.IsDone.Should().BeTrue();
    }

    [Fact]
    public void Checking_what_is_already_checked_raises_no_event()
    {
        var task = NewTask();
        var one = task.AddChecklistItem("Uno");
        task.UpdateChecklistItem(one.Id, done: true, text: null);
        task.ClearDomainEvents();

        task.UpdateChecklistItem(one.Id, done: true, text: null);

        task.DomainEvents.Should().BeEmpty("sin cambio real no hay nada que contar");
    }

    [Fact]
    public void Renaming_an_item_does_not_uncheck_it()
    {
        var task = NewTask();
        var one = task.AddChecklistItem("Con typo");
        task.UpdateChecklistItem(one.Id, done: true, text: null);

        task.UpdateChecklistItem(one.Id, done: null, text: "Sin typo");

        var item = task.Checklist.Single();
        item.Text.Should().Be("Sin typo");
        item.IsDone.Should().BeTrue();
    }

    [Fact]
    public void Touching_a_missing_item_is_rejected()
    {
        var task = NewTask();

        var check = () => task.UpdateChecklistItem(Guid.NewGuid(), true, null);
        var remove = () => task.RemoveChecklistItem(Guid.NewGuid());

        check.Should().Throw<InvalidOperationException>().WithMessage("*no existe*");
        remove.Should().Throw<InvalidOperationException>().WithMessage("*no existe*");
    }

    #endregion

    #region Recurrencia

    private static WorkTask RepeatingTask(string frequency, int interval, DateOnly from, DateOnly? end = null)
    {
        var task = NewTask();
        task.SetRecurrence(frequency, interval, from, end);
        return task;
    }

    [Fact]
    public void A_task_does_not_repeat_by_default()
    {
        NewTask().Recurrence.Should().BeNull();
    }

    [Fact]
    public void Before_the_date_nothing_is_generated()
    {
        var task = RepeatingTask(RecurrencePattern.Frequencies.Daily, 1, new DateOnly(2026, 8, 20));

        task.GenerateOccurrencesUntil(new DateOnly(2026, 8, 19)).Should().BeEmpty();
    }

    [Fact]
    public void All_overdue_occurrences_are_generated_at_once()
    {
        // Si la aplicación estuvo parada, saltarse las atrasadas dejaría huecos que nadie va a
        // reclamar pero que falsean cualquier informe.
        var task = RepeatingTask(RecurrencePattern.Frequencies.Daily, 1, new DateOnly(2026, 8, 10));

        var generated = task.GenerateOccurrencesUntil(new DateOnly(2026, 8, 13));

        generated.Should().HaveCount(4);
        generated.Select(t => t.DueDate).Should().ContainInOrder(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 11),
            new DateOnly(2026, 8, 12), new DateOnly(2026, 8, 13));
        task.Recurrence!.NextOccurrence.Should().Be(new DateOnly(2026, 8, 14));
    }

    [Fact]
    public void Occurrences_do_not_inherit_the_recurrence()
    {
        // Si la heredaran, cada ocurrencia empezaría a generar las suyas y la serie se
        // multiplicaría sola hasta llenar el tablero.
        var task = RepeatingTask(RecurrencePattern.Frequencies.Daily, 1, new DateOnly(2026, 8, 12));

        var generated = task.GenerateOccurrencesUntil(new DateOnly(2026, 8, 12));

        generated.Should().ContainSingle().Which.Recurrence.Should().BeNull();
    }

    [Fact]
    public void The_end_date_cuts_the_series()
    {
        var task = RepeatingTask(
            RecurrencePattern.Frequencies.Daily, 1,
            new DateOnly(2026, 8, 10), end: new DateOnly(2026, 8, 11));

        var generated = task.GenerateOccurrencesUntil(new DateOnly(2026, 8, 31));

        generated.Should().HaveCount(2);
        task.Recurrence!.IsExhausted.Should().BeTrue();
        task.GenerateOccurrencesUntil(new DateOnly(2026, 9, 30)).Should().BeEmpty();
    }

    [Fact]
    public void Each_occurrence_copies_the_template_work()
    {
        var task = NewTask();
        var companero = Guid.NewGuid();
        task.AddAssignee(companero);
        task.AddChecklistItem("Preparar sala");
        var item = task.AddChecklistItem("Enviar acta");
        task.UpdateChecklistItem(item.Id, done: true, text: null);
        task.Reprioritize("High");
        task.SetRecurrence(RecurrencePattern.Frequencies.Weekly, 1, new DateOnly(2026, 8, 12), null);

        var occurrence = task.GenerateOccurrencesUntil(new DateOnly(2026, 8, 12)).Single();

        occurrence.Title.Value.Should().Be(task.Title.Value);
        occurrence.Priority.Value.Should().Be("High");
        occurrence.Assignees.Select(a => a.UserId).Should().BeEquivalentTo(task.Assignees.Select(a => a.UserId));
        occurrence.Checklist.Select(p => p.Text).Should().BeEquivalentTo(["Preparar sala", "Enviar acta"]);
        occurrence.Checklist.Should().OnlyContain(p => !p.IsDone,
            "la copia empieza sin marcar; heredar lo hecho daría por completado trabajo que no se ha tocado");
    }

    [Fact]
    public void Occurrences_do_not_copy_the_parent_nor_hang_from_it()
    {
        var task = NewTask();
        task.Reparent(Guid.NewGuid());
        task.SetRecurrence(RecurrencePattern.Frequencies.Daily, 1, new DateOnly(2026, 8, 12), null);

        var occurrence = task.GenerateOccurrencesUntil(new DateOnly(2026, 8, 12)).Single();

        occurrence.ParentTaskId.Should().BeNull();
    }

    [Fact]
    public void Stopping_the_repeat_ends_the_series()
    {
        var task = RepeatingTask(RecurrencePattern.Frequencies.Daily, 1, new DateOnly(2026, 8, 10));

        task.ClearRecurrence();

        task.Recurrence.Should().BeNull();
        task.GenerateOccurrencesUntil(new DateOnly(2026, 12, 31)).Should().BeEmpty();
    }

    #endregion

    #region Datos descriptivos

    /// <summary>
    /// Editar un campo suelto no puede borrar los demás.
    ///
    /// Es lo que hace la tabla y el detalle de tarea: se manda sólo lo que cambió. Si lo que no
    /// viene se interpretase como «ponlo a vacío», cambiar la fecha borraría el título.
    /// </summary>
    [Fact]
    public void Updating_one_field_leaves_the_others_untouched()
    {
        var task = NewTask();

        task.UpdateDetails(estimatedHours: 13m);

        task.EstimatedHours.Should().Be(13m);
        task.Title.Value.Should().Be("Tarea de prueba");
        task.Description.Should().Be("descripción");
    }

    [Fact]
    public void Update_changes_title_description_hours_and_date()
    {
        var task = NewTask();
        var date = new DateOnly(2027, 1, 15);

        task.UpdateDetails("Otro título", "otra descripción", 3.5m, date);

        task.Title.Value.Should().Be("Otro título");
        task.Description.Should().Be("otra descripción");
        task.EstimatedHours.Should().Be(3.5m);
        task.DueDate.Should().Be(date);
    }

    [Fact]
    public void An_empty_title_is_rejected_instead_of_leaving_the_task_unnamed()
    {
        var task = NewTask();

        var act = () => task.UpdateDetails(title: "   ");

        act.Should().Throw<InvalidOperationException>();
        task.Title.Value.Should().Be("Tarea de prueba");
    }

    [Fact]
    public void A_too_long_title_is_rejected()
    {
        var task = NewTask();

        var act = () => task.UpdateDetails(title: new string('x', 201));

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Las horas negativas envenenarían cualquier suma de carga de trabajo —la vista que llega
    /// en el 4C— sin que nadie lo notase hasta que el total saliera mal.
    /// </summary>
    [Fact]
    public void Negative_hours_are_rejected()
    {
        var task = NewTask();

        var act = () => task.UpdateDetails(estimatedHours: -1m);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(WorkTask.DetailRules.NegativeHours);
        task.EstimatedHours.Should().Be(8m);
    }

    [Fact]
    public void Zero_hours_is_valid()
    {
        var task = NewTask();

        task.UpdateDetails(estimatedHours: 0m);

        task.EstimatedHours.Should().Be(0m);
    }

    /// <summary>
    /// Una descripción vacía sí es un valor: es como se borra. Distinguirla de «no la mandes»
    /// es justo lo que hace que el parámetro sea `null` y no cadena vacía.
    /// </summary>
    [Fact]
    public void The_description_can_be_cleared()
    {
        var task = NewTask();

        task.UpdateDetails(description: string.Empty);

        task.Description.Should().BeEmpty();
    }

    /// <summary>
    /// Cambiar datos descriptivos no emite eventos: no hay ninguna automatización de la 4D
    /// pensada para «alguien corrigió una errata», y emitirlos las haría trabajar de balde.
    /// </summary>
    [Fact]
    public void Updating_details_raises_no_domain_events()
    {
        var task = NewTask();
        task.ClearDomainEvents();

        task.UpdateDetails("Otro título", "otra", 1m, new DateOnly(2027, 3, 1));

        task.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Fecha de inicio

    private static WorkTask DueTask(DateOnly dueDate, DateOnly? start = null) => WorkTask.Create(
        tenantId: Guid.NewGuid(),
        projectId: Guid.NewGuid(),
        title: "Tarea con calendario",
        description: "descripción",
        assigneeId: Guid.NewGuid(),
        createdById: Guid.NewGuid(),
        estimatedHours: 8m,
        dueDate: dueDate,
        startDate: start);

    /// <summary>
    /// Una tarea sin fecha de inicio no se la inventa. Deducirla de la creación, o restando las
    /// horas al vencimiento, dibujaría en el Gantt una barra que nadie ha decidido.
    /// </summary>
    [Fact]
    public void A_task_starts_without_start_date_unless_given()
    {
        NewTask().StartDate.Should().BeNull();
    }

    [Fact]
    public void The_start_date_is_kept_on_create()
    {
        var task = DueTask(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 1));

        task.StartDate.Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public void The_same_start_and_due_day_is_valid()
    {
        var day = new DateOnly(2026, 9, 1);

        DueTask(day, day).StartDate.Should().Be(day);
    }

    [Fact]
    public void A_task_cannot_start_after_its_due_date()
    {
        var act = () => DueTask(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(WorkTask.DetailRules.StartAfterDueDate);
    }

    [Fact]
    public void Setting_a_start_after_the_due_date_is_rejected()
    {
        var task = DueTask(new DateOnly(2026, 9, 1));

        var act = () => task.UpdateDetails(startDate: new DateOnly(2026, 9, 10));

        act.Should().Throw<InvalidOperationException>();
        task.StartDate.Should().BeNull();
    }

    /// <summary>
    /// Adelantar el vencimiento por detrás del inicio dejaría una barra de longitud negativa. Se
    /// rechaza el cambio entero en lugar de recolocar fechas por cuenta propia: mover la
    /// planificación de alguien sin decírselo es peor que no dejarle hacer el cambio.
    /// </summary>
    [Fact]
    public void Moving_the_due_date_before_the_start_is_rejected()
    {
        var task = DueTask(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 5));

        var act = () => task.UpdateDetails(dueDate: new DateOnly(2026, 9, 1));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(WorkTask.DetailRules.DueDateBeforeStart);
    }

    /// <summary>
    /// Mandar las dos fechas a la vez se valida contra los valores nuevos, no contra los viejos:
    /// mover la tarea entera hacia adelante es legítimo y no puede fallar por el orden.
    /// </summary>
    [Fact]
    public void Moving_both_dates_forward_together_is_valid()
    {
        var task = DueTask(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 5));

        task.UpdateDetails(
            dueDate: new DateOnly(2026, 10, 10),
            startDate: new DateOnly(2026, 10, 5));

        task.StartDate.Should().Be(new DateOnly(2026, 10, 5));
        task.DueDate.Should().Be(new DateOnly(2026, 10, 10));
    }

    [Fact]
    public void The_start_date_can_be_removed()
    {
        var task = DueTask(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 5));

        task.UpdateDetails(clearStartDate: true);

        task.StartDate.Should().BeNull();
    }

    [Fact]
    public void Editing_another_field_keeps_the_start_date()
    {
        var task = DueTask(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 5));

        task.UpdateDetails(title: "Otro título");

        task.StartDate.Should().Be(new DateOnly(2026, 9, 5));
    }

    /// <summary>
    /// La ocurrencia hereda la **duración**, no la fecha literal: si la plantilla dura tres días,
    /// cada repetición dura tres días contra su propio vencimiento. Copiar el inicio tal cual
    /// dejaría ocurrencias que empiezan meses antes de vencer.
    /// </summary>
    [Fact]
    public void An_occurrence_inherits_the_duration_not_the_start_date()
    {
        var task = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Repetitiva", "descripción",
            Guid.NewGuid(), Guid.NewGuid(), 4m,
            dueDate: new DateOnly(2026, 8, 10), priority: null, parentTaskId: null,
            startDate: new DateOnly(2026, 8, 7));

        task.SetRecurrence(RecurrencePattern.Frequencies.Monthly, 1, new DateOnly(2026, 9, 10), null);

        var occurrence = task.GenerateOccurrencesUntil(new DateOnly(2026, 9, 10)).Single();

        occurrence.DueDate.Should().Be(new DateOnly(2026, 9, 10));
        occurrence.StartDate.Should().Be(new DateOnly(2026, 9, 7), "dura los mismos tres días");
    }

    [Fact]
    public void An_occurrence_of_a_task_without_start_has_none_either()
    {
        var task = RepeatingTask(RecurrencePattern.Frequencies.Daily, 1, new DateOnly(2026, 8, 10));

        var occurrence = task.GenerateOccurrencesUntil(new DateOnly(2026, 8, 10)).First();

        occurrence.StartDate.Should().BeNull();
    }

    #endregion

    #region Marcas de tiempo

    // De estas dos columnas salen el tiempo de entrega y el diagrama de quemado. Antes no
    // existían, y el panel devolvía 2,5 y 1,4 días escritos a mano porque no había con qué
    // calcularlos.

    private static WorkTask FreshTask() => WorkTask.Create(
        Guid.NewGuid(), Guid.NewGuid(), "Una tarea", "descripción",
        Guid.NewGuid(), Guid.NewGuid(), 3m, new DateOnly(2026, 12, 31));

    [Fact]
    public void A_task_starts_with_creation_date_and_no_completion_date()
    {
        var before = DateTime.UtcNow;

        var task = FreshTask();

        task.CreatedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        task.CompletedAtUtc.Should().BeNull("acaba de crearse, no está terminada");
    }

    [Fact]
    public void Finishing_a_task_records_when()
    {
        var task = FreshTask();
        var before = DateTime.UtcNow;

        task.Move("Done");

        task.CompletedAtUtc.Should().NotBeNull();
        task.CompletedAtUtc!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    /// <summary>
    /// Reabrir borra la marca. Es lo que hace que el tiempo de ciclo signifique algo: una tarea
    /// que se dio por hecha y se deshizo no está terminada, y conservar la fecha del primer
    /// cierre mediría un trabajo que luego hubo que rehacer.
    /// </summary>
    [Fact]
    public void Reopening_a_task_clears_the_completion_date()
    {
        var task = FreshTask();
        task.Move("Done");
        task.CompletedAtUtc.Should().NotBeNull();

        task.Move("In Progress");

        task.CompletedAtUtc.Should().BeNull("una tarea reabierta no está completada");
    }

    /// <summary>
    /// «En Espera» no es terminar. Contarla como cerrada falsearía el avance y el tiempo de
    /// entrega a la vez, y en la dirección que hace quedar bien, que es la peligrosa.
    /// </summary>
    [Theory]
    [InlineData("To Do")]
    [InlineData("In Progress")]
    [InlineData("In Review")]
    [InlineData("On Hold")]
    public void No_other_status_marks_the_task_as_done(string status)
    {
        var task = FreshTask();

        task.Move(status);

        task.CompletedAtUtc.Should().BeNull();
    }

    #endregion
}
