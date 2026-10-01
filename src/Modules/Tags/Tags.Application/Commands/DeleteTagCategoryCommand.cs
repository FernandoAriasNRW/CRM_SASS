using BuildingBlocks.Application.Abstractions;

namespace Tags.Application.Commands;

/// <summary>
/// Borra una categoría propia vacía. Con etiquetas dentro se rechaza: borrarlas en cascada
/// las quitaría también de las tareas y tickets que las llevan, y es demasiado para un clic que
/// parece de orden. Primero se mueven o se borran, una a una.
/// </summary>
public sealed record DeleteTagCategoryCommand(Guid TenantId, Guid UserId, Guid CategoryId) : ICommand;
