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

    #region Responsables

    [Fact]
    public void Una_tarea_creada_con_responsable_lo_tiene_tambien_en_la_coleccion()
    {
        // La invariante que sostiene todo lo demás: el principal siempre figura entre los
        // responsables. Si no, ninguna vista de las nuevas encontraría la tarea.
        var responsable = Guid.NewGuid();

        var tarea = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Con responsable", "x",
            responsable, Guid.NewGuid(), 1m, DateOnly.FromDateTime(DateTime.UtcNow));

        tarea.AssigneeId.Should().Be(responsable);
        tarea.Assignees.Select(a => a.UserId).Should().ContainSingle().Which.Should().Be(responsable);
        tarea.EsResponsable(responsable).Should().BeTrue();
    }

    [Fact]
    public void Una_tarea_sin_asignar_no_tiene_responsables()
    {
        var tarea = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Sin asignar", "x",
            Guid.Empty, Guid.NewGuid(), 1m, DateOnly.FromDateTime(DateTime.UtcNow));

        tarea.AssigneeId.Should().Be(Guid.Empty);
        tarea.Assignees.Should().BeEmpty("el Guid vacío significa «sin asignar», no una persona");
    }

    [Fact]
    public void Añadir_responsables_no_cambia_quien_es_el_principal()
    {
        var tarea = NuevaTarea();
        var principal = tarea.AssigneeId;
        var otro = Guid.NewGuid();

        tarea.AddAssignee(otro);

        tarea.AssigneeId.Should().Be(principal);
        tarea.Assignees.Select(a => a.UserId).Should().BeEquivalentTo([principal, otro]);
    }

    [Fact]
    public void La_primera_persona_de_una_tarea_sin_asignar_pasa_a_ser_la_principal()
    {
        // Lo contrario dejaría el campo del principal vacío con responsables dentro, que es
        // exactamente la incoherencia que la colección viene a evitar.
        var tarea = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Sin asignar", "x",
            Guid.Empty, Guid.NewGuid(), 1m, DateOnly.FromDateTime(DateTime.UtcNow));
        var alguien = Guid.NewGuid();

        tarea.AddAssignee(alguien);

        tarea.AssigneeId.Should().Be(alguien);
    }

    [Fact]
    public void La_misma_persona_no_se_puede_añadir_dos_veces()
    {
        var tarea = NuevaTarea();

        var repetir = () => tarea.AddAssignee(tarea.AssigneeId);

        repetir.Should().Throw<InvalidOperationException>().WithMessage("*ya es responsable*");
        tarea.Assignees.Should().HaveCount(1);
    }

    [Fact]
    public void Quitar_al_principal_promueve_al_siguiente()
    {
        // Sin promoción, la tarea quedaría con un principal que ya no es responsable.
        var tarea = NuevaTarea();
        var principal = tarea.AssigneeId;
        var segundo = Guid.NewGuid();
        tarea.AddAssignee(segundo);

        tarea.RemoveAssignee(principal);

        tarea.AssigneeId.Should().Be(segundo);
        tarea.Assignees.Select(a => a.UserId).Should().ContainSingle().Which.Should().Be(segundo);
    }

    [Fact]
    public void Quitar_al_ultimo_responsable_deja_la_tarea_sin_asignar()
    {
        var tarea = NuevaTarea();

        tarea.RemoveAssignee(tarea.AssigneeId);

        tarea.AssigneeId.Should().Be(Guid.Empty);
        tarea.Assignees.Should().BeEmpty();
    }

    [Fact]
    public void Quitar_a_quien_no_es_responsable_se_rechaza()
    {
        var tarea = NuevaTarea();

        var quitar = () => tarea.RemoveAssignee(Guid.NewGuid());

        quitar.Should().Throw<InvalidOperationException>().WithMessage("*no es responsable*");
        tarea.Assignees.Should().HaveCount(1);
    }

    [Fact]
    public void Cambiar_el_principal_lo_mete_en_la_coleccion_y_lo_pone_primero()
    {
        var tarea = NuevaTarea();
        var nuevo = Guid.NewGuid();

        tarea.Assign(nuevo);

        tarea.AssigneeId.Should().Be(nuevo);
        tarea.EsResponsable(nuevo).Should().BeTrue("el principal figura siempre entre los responsables");
        tarea.Assignees.Should().HaveCount(2, "el anterior sigue siendo responsable, sólo deja de ser el principal");
    }

    [Fact]
    public void Ascender_a_un_responsable_que_ya_estaba_no_lo_duplica()
    {
        var tarea = NuevaTarea();
        var segundo = Guid.NewGuid();
        tarea.AddAssignee(segundo);

        tarea.Assign(segundo);

        tarea.Assignees.Select(a => a.UserId).Should().HaveCount(2).And.OnlyHaveUniqueItems();
        tarea.AssigneeId.Should().Be(segundo);
    }

    [Fact]
    public void Desasignar_del_todo_vacia_la_coleccion()
    {
        var tarea = NuevaTarea();
        tarea.AddAssignee(Guid.NewGuid());

        tarea.Assign(Guid.Empty);

        tarea.AssigneeId.Should().Be(Guid.Empty);
        tarea.Assignees.Should().BeEmpty("«sin asignar» no puede convivir con responsables dentro");
    }

    [Fact]
    public void Añadir_y_quitar_responsables_emite_sus_eventos()
    {
        var tarea = NuevaTarea();
        var alguien = Guid.NewGuid();
        tarea.ClearDomainEvents();

        tarea.AddAssignee(alguien);
        tarea.RemoveAssignee(alguien);

        tarea.DomainEvents.Should().HaveCount(2);
        tarea.DomainEvents.First().Should().BeOfType<TaskAssigneeAddedEvent>();
        tarea.DomainEvents.Last().Should().BeOfType<TaskAssigneeRemovedEvent>();
    }

    #endregion

    #region Checklist

    [Fact]
    public void Una_tarea_nace_sin_checklist()
    {
        NuevaTarea().Checklist.Should().BeEmpty();
    }

    [Fact]
    public void Los_puntos_se_añaden_al_final_con_posiciones_crecientes()
    {
        var tarea = NuevaTarea();

        tarea.AddChecklistItem("Primero");
        tarea.AddChecklistItem("Segundo");
        tarea.AddChecklistItem("Tercero");

        tarea.Checklist.OrderBy(i => i.Posicion).Select(i => i.Texto)
            .Should().ContainInOrder("Primero", "Segundo", "Tercero");
        tarea.Checklist.Select(i => i.Posicion).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Borrar_del_medio_no_hace_que_dos_puntos_empaten_en_el_orden()
    {
        // La posición se calcula sobre la mayor existente, no contando puntos: contarlos daría
        // una posición repetida en cuanto se borrara alguno del medio, y el orden dejaría de
        // estar definido.
        var tarea = NuevaTarea();
        tarea.AddChecklistItem("Primero");
        var delMedio = tarea.AddChecklistItem("Segundo");
        tarea.AddChecklistItem("Tercero");

        tarea.RemoveChecklistItem(delMedio.Id);
        tarea.AddChecklistItem("Cuarto");

        tarea.Checklist.Select(i => i.Posicion).Should().OnlyHaveUniqueItems();
        tarea.Checklist.OrderBy(i => i.Posicion).Last().Texto.Should().Be("Cuarto");
    }

    [Fact]
    public void Un_punto_sin_texto_no_se_acepta()
    {
        var tarea = NuevaTarea();

        var vacio = () => tarea.AddChecklistItem("   ");

        vacio.Should().Throw<InvalidOperationException>().WithMessage("*necesita un texto*");
        tarea.Checklist.Should().BeEmpty();
    }

    [Fact]
    public void Un_texto_demasiado_largo_no_se_acepta()
    {
        var tarea = NuevaTarea();

        var largo = () => tarea.AddChecklistItem(new string('x', ChecklistItem.LargoMaximo + 1));

        largo.Should().Throw<InvalidOperationException>().WithMessage("*no puede pasar de*");
    }

    [Fact]
    public void El_texto_se_recorta_al_guardarlo()
    {
        var tarea = NuevaTarea();

        var punto = tarea.AddChecklistItem("  con espacios  ");

        punto.Texto.Should().Be("con espacios");
    }

    [Fact]
    public void Marcar_un_punto_cuenta_en_el_progreso_y_emite_evento()
    {
        var tarea = NuevaTarea();
        var uno = tarea.AddChecklistItem("Uno");
        tarea.AddChecklistItem("Dos");
        tarea.ClearDomainEvents();

        tarea.UpdateChecklistItem(uno.Id, hecho: true, texto: null);

        tarea.ProgresoDeChecklist().Should().Be((2, 1));
        tarea.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskChecklistItemToggledEvent>()
            .Which.Hecho.Should().BeTrue();
    }

    [Fact]
    public void Marcar_lo_que_ya_estaba_marcado_no_emite_evento()
    {
        var tarea = NuevaTarea();
        var uno = tarea.AddChecklistItem("Uno");
        tarea.UpdateChecklistItem(uno.Id, hecho: true, texto: null);
        tarea.ClearDomainEvents();

        tarea.UpdateChecklistItem(uno.Id, hecho: true, texto: null);

        tarea.DomainEvents.Should().BeEmpty("sin cambio real no hay nada que contar");
    }

    [Fact]
    public void Renombrar_un_punto_no_lo_desmarca()
    {
        var tarea = NuevaTarea();
        var uno = tarea.AddChecklistItem("Con typo");
        tarea.UpdateChecklistItem(uno.Id, hecho: true, texto: null);

        tarea.UpdateChecklistItem(uno.Id, hecho: null, texto: "Sin typo");

        var punto = tarea.Checklist.Single();
        punto.Texto.Should().Be("Sin typo");
        punto.Hecho.Should().BeTrue();
    }

    [Fact]
    public void Tocar_un_punto_que_no_existe_se_rechaza()
    {
        var tarea = NuevaTarea();

        var marcar = () => tarea.UpdateChecklistItem(Guid.NewGuid(), true, null);
        var borrar = () => tarea.RemoveChecklistItem(Guid.NewGuid());

        marcar.Should().Throw<InvalidOperationException>().WithMessage("*no existe*");
        borrar.Should().Throw<InvalidOperationException>().WithMessage("*no existe*");
    }

    #endregion

    #region Recurrencia

    private static WorkTask TareaQueSeRepite(string frecuencia, int intervalo, DateOnly desde, DateOnly? fin = null)
    {
        var tarea = NuevaTarea();
        tarea.Repetir(frecuencia, intervalo, desde, fin);
        return tarea;
    }

    [Fact]
    public void Una_tarea_no_se_repite_por_defecto()
    {
        NuevaTarea().Recurrence.Should().BeNull();
    }

    [Fact]
    public void Sin_llegar_la_fecha_no_se_genera_nada()
    {
        var tarea = TareaQueSeRepite(PatronDeRecurrencia.Frecuencias.Diaria, 1, new DateOnly(2026, 8, 20));

        tarea.GenerarOcurrenciasHasta(new DateOnly(2026, 8, 19)).Should().BeEmpty();
    }

    [Fact]
    public void Se_generan_todas_las_atrasadas_de_una_vez()
    {
        // Si la aplicación estuvo parada, saltarse las atrasadas dejaría huecos que nadie va a
        // reclamar pero que falsean cualquier informe.
        var tarea = TareaQueSeRepite(PatronDeRecurrencia.Frecuencias.Diaria, 1, new DateOnly(2026, 8, 10));

        var generadas = tarea.GenerarOcurrenciasHasta(new DateOnly(2026, 8, 13));

        generadas.Should().HaveCount(4);
        generadas.Select(t => t.DueDate).Should().ContainInOrder(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 11),
            new DateOnly(2026, 8, 12), new DateOnly(2026, 8, 13));
        tarea.Recurrence!.ProximaOcurrencia.Should().Be(new DateOnly(2026, 8, 14));
    }

    [Fact]
    public void Las_ocurrencias_no_heredan_la_recurrencia()
    {
        // Si la heredaran, cada ocurrencia empezaría a generar las suyas y la serie se
        // multiplicaría sola hasta llenar el tablero.
        var tarea = TareaQueSeRepite(PatronDeRecurrencia.Frecuencias.Diaria, 1, new DateOnly(2026, 8, 12));

        var generadas = tarea.GenerarOcurrenciasHasta(new DateOnly(2026, 8, 12));

        generadas.Should().ContainSingle().Which.Recurrence.Should().BeNull();
    }

    [Fact]
    public void La_fecha_de_fin_corta_la_serie()
    {
        var tarea = TareaQueSeRepite(
            PatronDeRecurrencia.Frecuencias.Diaria, 1,
            new DateOnly(2026, 8, 10), fin: new DateOnly(2026, 8, 11));

        var generadas = tarea.GenerarOcurrenciasHasta(new DateOnly(2026, 8, 31));

        generadas.Should().HaveCount(2);
        tarea.Recurrence!.Agotado.Should().BeTrue();
        tarea.GenerarOcurrenciasHasta(new DateOnly(2026, 9, 30)).Should().BeEmpty();
    }

    [Fact]
    public void Cada_ocurrencia_copia_el_trabajo_de_la_plantilla()
    {
        var tarea = NuevaTarea();
        var companero = Guid.NewGuid();
        tarea.AddAssignee(companero);
        tarea.AddChecklistItem("Preparar sala");
        var punto = tarea.AddChecklistItem("Enviar acta");
        tarea.UpdateChecklistItem(punto.Id, hecho: true, texto: null);
        tarea.Reprioritize("High");
        tarea.Repetir(PatronDeRecurrencia.Frecuencias.Semanal, 1, new DateOnly(2026, 8, 12), null);

        var ocurrencia = tarea.GenerarOcurrenciasHasta(new DateOnly(2026, 8, 12)).Single();

        ocurrencia.Title.Value.Should().Be(tarea.Title.Value);
        ocurrencia.Priority.Value.Should().Be("High");
        ocurrencia.Assignees.Select(a => a.UserId).Should().BeEquivalentTo(tarea.Assignees.Select(a => a.UserId));
        ocurrencia.Checklist.Select(p => p.Texto).Should().BeEquivalentTo(["Preparar sala", "Enviar acta"]);
        ocurrencia.Checklist.Should().OnlyContain(p => !p.Hecho,
            "la copia empieza sin marcar; heredar lo hecho daría por completado trabajo que no se ha tocado");
    }

    [Fact]
    public void Las_ocurrencias_no_copian_el_padre_ni_quedan_colgadas()
    {
        var tarea = NuevaTarea();
        tarea.Reparent(Guid.NewGuid());
        tarea.Repetir(PatronDeRecurrencia.Frecuencias.Diaria, 1, new DateOnly(2026, 8, 12), null);

        var ocurrencia = tarea.GenerarOcurrenciasHasta(new DateOnly(2026, 8, 12)).Single();

        ocurrencia.ParentTaskId.Should().BeNull();
    }

    [Fact]
    public void Dejar_de_repetir_para_la_serie()
    {
        var tarea = TareaQueSeRepite(PatronDeRecurrencia.Frecuencias.Diaria, 1, new DateOnly(2026, 8, 10));

        tarea.DejarDeRepetir();

        tarea.Recurrence.Should().BeNull();
        tarea.GenerarOcurrenciasHasta(new DateOnly(2026, 12, 31)).Should().BeEmpty();
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
    public void Actualizar_un_solo_campo_deja_los_demas_como_estaban()
    {
        var tarea = NuevaTarea();

        tarea.ActualizarDetalles(horasEstimadas: 13m);

        tarea.EstimatedHours.Should().Be(13m);
        tarea.Title.Value.Should().Be("Tarea de prueba");
        tarea.Description.Should().Be("descripción");
    }

    [Fact]
    public void Actualizar_cambia_titulo_descripcion_horas_y_fecha()
    {
        var tarea = NuevaTarea();
        var fecha = new DateOnly(2027, 1, 15);

        tarea.ActualizarDetalles("Otro título", "otra descripción", 3.5m, fecha);

        tarea.Title.Value.Should().Be("Otro título");
        tarea.Description.Should().Be("otra descripción");
        tarea.EstimatedHours.Should().Be(3.5m);
        tarea.DueDate.Should().Be(fecha);
    }

    [Fact]
    public void Un_titulo_vacio_se_rechaza_en_lugar_de_dejar_la_tarea_sin_nombre()
    {
        var tarea = NuevaTarea();

        var accion = () => tarea.ActualizarDetalles(titulo: "   ");

        accion.Should().Throw<InvalidOperationException>();
        tarea.Title.Value.Should().Be("Tarea de prueba");
    }

    [Fact]
    public void Un_titulo_demasiado_largo_se_rechaza()
    {
        var tarea = NuevaTarea();

        var accion = () => tarea.ActualizarDetalles(titulo: new string('x', 201));

        accion.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Las horas negativas envenenarían cualquier suma de carga de trabajo —la vista que llega
    /// en el 4C— sin que nadie lo notase hasta que el total saliera mal.
    /// </summary>
    [Fact]
    public void Las_horas_negativas_se_rechazan()
    {
        var tarea = NuevaTarea();

        var accion = () => tarea.ActualizarDetalles(horasEstimadas: -1m);

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(WorkTask.ReglasDeDetalle.HorasNegativas);
        tarea.EstimatedHours.Should().Be(8m);
    }

    [Fact]
    public void Cero_horas_es_valido()
    {
        var tarea = NuevaTarea();

        tarea.ActualizarDetalles(horasEstimadas: 0m);

        tarea.EstimatedHours.Should().Be(0m);
    }

    /// <summary>
    /// Una descripción vacía sí es un valor: es como se borra. Distinguirla de «no la mandes»
    /// es justo lo que hace que el parámetro sea `null` y no cadena vacía.
    /// </summary>
    [Fact]
    public void La_descripcion_se_puede_vaciar()
    {
        var tarea = NuevaTarea();

        tarea.ActualizarDetalles(descripcion: string.Empty);

        tarea.Description.Should().BeEmpty();
    }

    /// <summary>
    /// Cambiar datos descriptivos no emite eventos: no hay ninguna automatización de la 4D
    /// pensada para «alguien corrigió una errata», y emitirlos las haría trabajar de balde.
    /// </summary>
    [Fact]
    public void Actualizar_detalles_no_emite_eventos_de_dominio()
    {
        var tarea = NuevaTarea();
        tarea.ClearDomainEvents();

        tarea.ActualizarDetalles("Otro título", "otra", 1m, new DateOnly(2027, 3, 1));

        tarea.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Fecha de inicio

    private static WorkTask TareaQueVence(DateOnly vencimiento, DateOnly? inicio = null) => WorkTask.Create(
        tenantId: Guid.NewGuid(),
        projectId: Guid.NewGuid(),
        title: "Tarea con calendario",
        description: "descripción",
        assigneeId: Guid.NewGuid(),
        createdById: Guid.NewGuid(),
        estimatedHours: 8m,
        dueDate: vencimiento,
        startDate: inicio);

    /// <summary>
    /// Una tarea sin fecha de inicio no se la inventa. Deducirla de la creación, o restando las
    /// horas al vencimiento, dibujaría en el Gantt una barra que nadie ha decidido.
    /// </summary>
    [Fact]
    public void Una_tarea_nace_sin_fecha_de_inicio_si_no_se_le_da_una()
    {
        NuevaTarea().StartDate.Should().BeNull();
    }

    [Fact]
    public void La_fecha_de_inicio_se_conserva_al_crear()
    {
        var tarea = TareaQueVence(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 1));

        tarea.StartDate.Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public void El_mismo_dia_de_inicio_y_vencimiento_vale()
    {
        var dia = new DateOnly(2026, 9, 1);

        TareaQueVence(dia, dia).StartDate.Should().Be(dia);
    }

    [Fact]
    public void No_se_puede_crear_una_tarea_que_empiece_despues_de_vencer()
    {
        var accion = () => TareaQueVence(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(WorkTask.ReglasDeDetalle.InicioDespuesDelVencimiento);
    }

    [Fact]
    public void Poner_un_inicio_posterior_al_vencimiento_se_rechaza()
    {
        var tarea = TareaQueVence(new DateOnly(2026, 9, 1));

        var accion = () => tarea.ActualizarDetalles(fechaInicio: new DateOnly(2026, 9, 10));

        accion.Should().Throw<InvalidOperationException>();
        tarea.StartDate.Should().BeNull();
    }

    /// <summary>
    /// Adelantar el vencimiento por detrás del inicio dejaría una barra de longitud negativa. Se
    /// rechaza el cambio entero en lugar de recolocar fechas por cuenta propia: mover la
    /// planificación de alguien sin decírselo es peor que no dejarle hacer el cambio.
    /// </summary>
    [Fact]
    public void Adelantar_el_vencimiento_por_detras_del_inicio_se_rechaza()
    {
        var tarea = TareaQueVence(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 5));

        var accion = () => tarea.ActualizarDetalles(fechaLimite: new DateOnly(2026, 9, 1));

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage(WorkTask.ReglasDeDetalle.VencimientoAntesDelInicio);
    }

    /// <summary>
    /// Mandar las dos fechas a la vez se valida contra los valores nuevos, no contra los viejos:
    /// mover la tarea entera hacia adelante es legítimo y no puede fallar por el orden.
    /// </summary>
    [Fact]
    public void Mover_las_dos_fechas_a_la_vez_hacia_adelante_vale()
    {
        var tarea = TareaQueVence(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 5));

        tarea.ActualizarDetalles(
            fechaLimite: new DateOnly(2026, 10, 10),
            fechaInicio: new DateOnly(2026, 10, 5));

        tarea.StartDate.Should().Be(new DateOnly(2026, 10, 5));
        tarea.DueDate.Should().Be(new DateOnly(2026, 10, 10));
    }

    [Fact]
    public void La_fecha_de_inicio_se_puede_quitar()
    {
        var tarea = TareaQueVence(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 5));

        tarea.ActualizarDetalles(quitarFechaInicio: true);

        tarea.StartDate.Should().BeNull();
    }

    [Fact]
    public void Editar_otro_campo_no_toca_la_fecha_de_inicio()
    {
        var tarea = TareaQueVence(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 5));

        tarea.ActualizarDetalles(titulo: "Otro título");

        tarea.StartDate.Should().Be(new DateOnly(2026, 9, 5));
    }

    /// <summary>
    /// La ocurrencia hereda la **duración**, no la fecha literal: si la plantilla dura tres días,
    /// cada repetición dura tres días contra su propio vencimiento. Copiar el inicio tal cual
    /// dejaría ocurrencias que empiezan meses antes de vencer.
    /// </summary>
    [Fact]
    public void Una_ocurrencia_hereda_la_duracion_y_no_la_fecha_de_inicio()
    {
        var tarea = WorkTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Repetitiva", "descripción",
            Guid.NewGuid(), Guid.NewGuid(), 4m,
            dueDate: new DateOnly(2026, 8, 10), priority: null, parentTaskId: null,
            startDate: new DateOnly(2026, 8, 7));

        tarea.Repetir(PatronDeRecurrencia.Frecuencias.Mensual, 1, new DateOnly(2026, 9, 10), null);

        var ocurrencia = tarea.GenerarOcurrenciasHasta(new DateOnly(2026, 9, 10)).Single();

        ocurrencia.DueDate.Should().Be(new DateOnly(2026, 9, 10));
        ocurrencia.StartDate.Should().Be(new DateOnly(2026, 9, 7), "dura los mismos tres días");
    }

    [Fact]
    public void Una_ocurrencia_de_una_tarea_sin_inicio_tampoco_lo_tiene()
    {
        var tarea = TareaQueSeRepite(PatronDeRecurrencia.Frecuencias.Diaria, 1, new DateOnly(2026, 8, 10));

        var ocurrencia = tarea.GenerarOcurrenciasHasta(new DateOnly(2026, 8, 10)).First();

        ocurrencia.StartDate.Should().BeNull();
    }

    #endregion
}
