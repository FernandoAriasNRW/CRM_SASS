using FluentAssertions;
using WorkItems.Domain.Entities;
using WorkItems.Domain.Events;
using Xunit;

namespace UnitTests;

/// <summary>
/// Invariantes de WorkTask. Se prueban contra el agregado directamente, sin base de
/// datos: la máquina de estados es una regla de negocio y debe sostenerse por sí sola,
/// aunque cambie la persistencia.
/// </summary>
public sealed class WorkTaskInvariantsTests
{
    private static WorkTask NuevaTarea() => WorkTask.Create(
        tenantId: Guid.NewGuid(),
        projectId: Guid.NewGuid(),
        title: "Tarea de prueba",
        description: "descripción",
        assigneeId: Guid.NewGuid(),
        createdById: Guid.NewGuid(),
        estimatedHours: 8m,
        dueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

    [Fact]
    public void Una_tarea_nace_en_To_Do_y_emite_evento_de_creacion()
    {
        var tarea = NuevaTarea();

        tarea.Status.Value.ToString().Should().Be("To Do");
        tarea.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TaskCreatedEvent>();
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
    [MemberData(nameof(TodasLasCombinaciones))]
    public void Cualquier_estado_es_alcanzable_desde_cualquier_otro(string desde, string hasta)
    {
        var tarea = NuevaTarea();
        tarea.Move(desde);

        tarea.Move(hasta);

        tarea.Status.Value.ToString().Should().Be(hasta);
    }

    public static TheoryData<string, string> TodasLasCombinaciones()
    {
        var estados = new[] { "To Do", "In Progress", "In Review", "Done", "On Hold" };
        var datos = new TheoryData<string, string>();
        foreach (var desde in estados)
            foreach (var hasta in estados)
                datos.Add(desde, hasta);
        return datos;
    }

    [Fact]
    public void Un_estado_inexistente_no_se_acepta()
    {
        // Lo único que sigue rechazándose. No es política de flujo: un estado que no
        // existe es un dato corrupto, y aceptarlo dejaría la tarea en un limbo que
        // ninguna vista sabría representar.
        var tarea = NuevaTarea();

        var mover = () => tarea.Move("Archivada");

        mover.Should().Throw<InvalidOperationException>()
            .WithMessage("*no existe*");
    }

    [Fact]
    public void Mover_emite_el_evento_con_el_estado_anterior_y_el_nuevo()
    {
        var tarea = NuevaTarea();
        tarea.ClearDomainEvents();

        tarea.Move("In Progress");

        var evento = tarea.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskStatusChangedEvent>().Subject;
        evento.OldStatus.Should().Be("To Do");
        evento.NewStatus.Should().Be("In Progress");
    }

    [Fact]
    public void Reasignar_emite_evento_de_asignacion()
    {
        var tarea = NuevaTarea();
        tarea.ClearDomainEvents();
        var nuevoResponsable = Guid.NewGuid();

        tarea.Assign(nuevoResponsable);

        tarea.AssigneeId.Should().Be(nuevoResponsable);
        tarea.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TaskAssignedEvent>();
    }

    [Fact]
    public void Añadir_la_misma_etiqueta_dos_veces_no_la_duplica()
    {
        var tarea = NuevaTarea();
        var etiqueta = Guid.NewGuid();

        tarea.AddTag(etiqueta);
        tarea.AddTag(etiqueta);

        tarea.TagIds.Should().ContainSingle().Which.Should().Be(etiqueta);
    }

    [Fact]
    public void Quitar_una_etiqueta_que_no_esta_no_falla()
    {
        var tarea = NuevaTarea();

        var quitar = () => tarea.RemoveTag(Guid.NewGuid());

        quitar.Should().NotThrow();
        tarea.TagIds.Should().BeEmpty();
    }

}
