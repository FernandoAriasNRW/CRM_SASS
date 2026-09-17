using BuildingBlocks.Application;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions;
using WorkItems.Application.Abstractions.Repositories;

namespace WorkItems.Application;

/// <summary>
/// Archivar, desarchivar, tirar a la papelera y restaurar una tarea.
///
/// Un solo comando con una acción, y no cuatro comandos: las cuatro hacen lo mismo —cargar la
/// tarea, cambiarle un campo, guardar— y separarlas serían cuatro copias del mismo handler
/// esperando a divergir.
/// </summary>
public sealed record ChangeTaskArchiveStateCommand(
    Guid TenantId,
    Guid Id,
    ArchiveAction Action) : ICommand<bool>;
