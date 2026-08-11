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

    [Theory]
    [InlineData("To Do", "In Progress")]
    [InlineData("To Do", "Done")]
    [InlineData("In Progress", "In Review")]
    [InlineData("In Progress", "On Hold")]
    [InlineData("In Review", "Done")]
    [InlineData("Done", "To Do")]
    public void Las_transiciones_permitidas_cambian_el_estado(string desde, string hasta)
    {
        var tarea = NuevaTarea();
        LlevarA(tarea, desde);

        tarea.Move(hasta);

        tarea.Status.Value.ToString().Should().Be(hasta);
    }

    [Theory]
    [InlineData("To Do", "In Review")]
    [InlineData("To Do", "On Hold")]
    [InlineData("In Review", "On Hold")]
    [InlineData("Done", "In Progress")]
    [InlineData("On Hold", "Done")]
    public void Las_transiciones_no_permitidas_se_rechazan(string desde, string hasta)
    {
        var tarea = NuevaTarea();
        LlevarA(tarea, desde);

        var mover = () => tarea.Move(hasta);

        mover.Should().Throw<InvalidOperationException>()
            .WithMessage($"*'{hasta}'*");
    }

    [Fact]
    public void Un_estado_inexistente_no_se_acepta()
    {
        var tarea = NuevaTarea();

        var mover = () => tarea.Move("Archivada");

        mover.Should().Throw<InvalidOperationException>();
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

    /// <summary>
    /// Recorre la máquina de estados hasta el estado pedido. No se puede construir una
    /// tarea directamente en un estado distinto de To Do, que es precisamente la
    /// invariante que se está protegiendo.
    /// </summary>
    private static void LlevarA(WorkTask tarea, string estado)
    {
        var camino = estado switch
        {
            "To Do" => Array.Empty<string>(),
            "In Progress" => ["In Progress"],
            "In Review" => new[] { "In Progress", "In Review" },
            "Done" => ["Done"],
            "On Hold" => ["In Progress", "On Hold"],
            _ => throw new ArgumentException($"Estado no contemplado: {estado}", nameof(estado))
        };

        foreach (var paso in camino)
            tarea.Move(paso);

        tarea.ClearDomainEvents();
    }
}
