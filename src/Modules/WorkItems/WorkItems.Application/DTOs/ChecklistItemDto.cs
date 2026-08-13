namespace WorkItems.Application.DTOs;

/// <summary>
/// Un punto de la checklist. Llega ya ordenado por su posición: el orden es del usuario y no del
/// almacenamiento, así que la consulta se encarga de respetarlo.
/// </summary>
public sealed record ChecklistItemDto(Guid Id, string Texto, bool Hecho, int Posicion);
