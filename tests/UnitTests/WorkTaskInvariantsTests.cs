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

    #region Prioridad

    [Fact]
    public void Una_tarea_sin_prioridad_explicita_nace_en_Normal()
    {
        var tarea = NuevaTarea();

        tarea.Priority.Value.Should().Be("Normal");
    }

    [Fact]
    public void Una_tarea_puede_nacer_con_la_prioridad_que_se_le_indique()
    {
        var tarea = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Urgente", "descripción",
            Guid.NewGuid(), Guid.NewGuid(), 4m,
            DateOnly.FromDateTime(DateTime.UtcNow), "Urgent");

        tarea.Priority.Value.Should().Be("Urgent");
    }

    /// <summary>
    /// Igual que con el estado, cualquier prioridad existente es alcanzable desde cualquier
    /// otra, y en los dos sentidos. Subir o bajar una tarea es de quien gestiona el trabajo:
    /// si alguien introduce una regla de «no se puede bajar de Urgente», este test la caza.
    /// </summary>
    [Theory]
    [MemberData(nameof(TodasLasCombinacionesDePrioridad))]
    public void Cualquier_prioridad_es_alcanzable_desde_cualquier_otra(string desde, string hasta)
    {
        var tarea = NuevaTarea();
        tarea.Reprioritize(desde);

        tarea.Reprioritize(hasta);

        tarea.Priority.Value.Should().Be(hasta);
    }

    public static TheoryData<string, string> TodasLasCombinacionesDePrioridad()
    {
        var prioridades = TaskPriority.All().Select(p => p.Value).ToArray();
        var datos = new TheoryData<string, string>();
        foreach (var desde in prioridades)
            foreach (var hasta in prioridades)
                datos.Add(desde, hasta);
        return datos;
    }

    [Fact]
    public void Una_prioridad_inexistente_no_se_acepta_al_repriorizar()
    {
        var tarea = NuevaTarea();

        var repriorizar = () => tarea.Reprioritize("Crítica");

        repriorizar.Should().Throw<InvalidOperationException>().WithMessage("*no existe*");
        tarea.Priority.Value.Should().Be("Normal", "una prioridad inválida no debe dejar la tarea a medias");
    }

    [Fact]
    public void Una_prioridad_inexistente_no_se_acepta_al_crear()
    {
        var crear = () => WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Tarea", "descripción",
            Guid.NewGuid(), Guid.NewGuid(), 1m,
            DateOnly.FromDateTime(DateTime.UtcNow), "Altísima");

        crear.Should().Throw<InvalidOperationException>().WithMessage("*no existe*");
    }

    [Fact]
    public void Repriorizar_emite_el_evento_con_la_anterior_y_la_nueva()
    {
        var tarea = NuevaTarea();
        tarea.ClearDomainEvents();

        tarea.Reprioritize("Urgent");

        var evento = tarea.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskPriorityChangedEvent>().Subject;
        evento.OldPriority.Should().Be("Normal");
        evento.NewPriority.Should().Be("Urgent");
    }

    [Fact]
    public void Repriorizar_a_la_que_ya_tiene_no_emite_evento()
    {
        // Sin cambio real no hay nada que contar. Un evento vacío haría trabajar de más a
        // las automatizaciones que se apoyarán en él.
        var tarea = NuevaTarea();
        tarea.ClearDomainEvents();

        tarea.Reprioritize("Normal");

        tarea.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void La_prioridad_conserva_su_nombre_en_español()
    {
        // TaskStatus construye el estado desde su valor al mover, y deja el nombre igual que
        // el valor: una tarea movida a «Done» acaba con nombre «Done» en lugar de
        // «Completado». La prioridad usa la instancia canónica para no repetirlo.
        var tarea = NuevaTarea();

        tarea.Reprioritize("Urgent");

        tarea.Priority.Name.Should().Be("Urgente");
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
    public void El_orden_de_las_prioridades_es_de_negocio()
    {
        TaskPriority.All().Select(p => p.Value)
            .Should().ContainInOrder("Urgent", "High", "Normal", "Low")
            .And.HaveCount(4);

        TaskPriority.OrdenDe("Urgent").Should().BeLessThan(TaskPriority.OrdenDe("Low"));
    }

    [Fact]
    public void Una_prioridad_desconocida_se_ordena_al_final()
    {
        // Cubre las filas antiguas que pudieran tener la columna vacía: deben caer al fondo,
        // no colarse en la cabecera como si fueran lo más urgente.
        TaskPriority.OrdenDe("").Should().BeGreaterThan(TaskPriority.OrdenDe("Low"));
    }

    #endregion

    #region Subtareas

    [Fact]
    public void Una_tarea_nace_de_primer_nivel()
    {
        var tarea = NuevaTarea();

        tarea.ParentTaskId.Should().BeNull();
        tarea.EsSubtarea.Should().BeFalse();
    }

    [Fact]
    public void Una_tarea_puede_nacer_como_subtarea()
    {
        var padre = Guid.NewGuid();

        var tarea = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Subtarea", "descripción",
            Guid.NewGuid(), Guid.NewGuid(), 1m,
            DateOnly.FromDateTime(DateTime.UtcNow), null, padre);

        tarea.ParentTaskId.Should().Be(padre);
        tarea.EsSubtarea.Should().BeTrue();
    }

    [Fact]
    public void Una_tarea_no_puede_ser_subtarea_de_si_misma()
    {
        // La única de las tres reglas de anidamiento que el agregado puede comprobar solo.
        var tarea = NuevaTarea();

        var colgar = () => tarea.Reparent(tarea.Id);

        colgar.Should().Throw<InvalidOperationException>().WithMessage("*de sí misma*");
        tarea.ParentTaskId.Should().BeNull();
    }

    [Fact]
    public void Colgar_de_otra_emite_el_evento_con_el_padre_anterior_y_el_nuevo()
    {
        var tarea = NuevaTarea();
        var primerPadre = Guid.NewGuid();
        var segundoPadre = Guid.NewGuid();
        tarea.Reparent(primerPadre);
        tarea.ClearDomainEvents();

        tarea.Reparent(segundoPadre);

        var evento = tarea.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskParentChangedEvent>().Subject;
        evento.OldParentTaskId.Should().Be(primerPadre);
        evento.NewParentTaskId.Should().Be(segundoPadre);
    }

    [Fact]
    public void Desligar_deja_la_tarea_de_primer_nivel_y_lo_cuenta()
    {
        var tarea = NuevaTarea();
        tarea.Reparent(Guid.NewGuid());
        tarea.ClearDomainEvents();

        tarea.Reparent(null);

        tarea.EsSubtarea.Should().BeFalse();
        tarea.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskParentChangedEvent>()
            .Which.NewParentTaskId.Should().BeNull();
    }

    [Fact]
    public void Colgar_del_padre_que_ya_tiene_no_emite_evento()
    {
        var tarea = NuevaTarea();
        var padre = Guid.NewGuid();
        tarea.Reparent(padre);
        tarea.ClearDomainEvents();

        tarea.Reparent(padre);

        tarea.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Un_identificador_de_padre_vacio_no_se_acepta()
    {
        // Guid.Empty no es «sin padre», es un dato mal formado: sin padre es null. Aceptarlo
        // dejaría una subtarea colgando de una tarea que no existe.
        var tarea = NuevaTarea();

        var colgar = () => tarea.Reparent(Guid.Empty);

        colgar.Should().Throw<InvalidOperationException>().WithMessage("*no es válido*");
    }

    [Fact]
    public void El_anidamiento_se_limita_a_un_nivel()
    {
        // Las reglas viven en un solo sitio para que el handler que las aplica no las
        // reinvente con otros mensajes.
        WorkTask.ReglasDeAnidamiento.ProfundidadMaxima.Should().Be(2);
        WorkTask.ReglasDeAnidamiento.PadreEsSubtarea.Should().Contain("un solo nivel");
    }

    #endregion
}
