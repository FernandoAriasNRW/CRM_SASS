namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Quién está en cada equipo, para los listados que filtran por equipo.
///
/// El tablero de un equipo enseña las tareas de sus miembros, y «de mi equipo» las de la gente con
/// la que comparto equipo. Las tareas no saben de equipos —viven en WorkItems y los equipos en
/// Teams—, así que la lista de personas se resuelve aquí y la consulta filtra por ella. Lo
/// implementa Teams.
/// </summary>
public interface ITeamDirectory
{
    /// <summary>Los miembros activos del equipo, o <c>null</c> si el equipo no existe en la organización.</summary>
    Task<IReadOnlyList<Guid>?> GetMemberIdsAsync(Guid tenantId, Guid teamId, CancellationToken ct = default);

    /// <summary>
    /// Las personas con las que comparto algún equipo, yo incluida. Vacía si no estoy en ninguno:
    /// filtrar por ella da cero resultados, que es lo que debe ver quien no tiene equipo.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetTeamMateIdsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
