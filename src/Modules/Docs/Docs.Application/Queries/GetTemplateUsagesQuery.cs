using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Queries;

/// <summary>
/// El uso de las plantillas del inquilino, para ordenar la galería.
///
/// Devuelve sólo los contadores, no el catálogo. Las plantillas predefinidas se pintan con
/// nombre, icono y color, y eso es presentación: si el servidor mandara los títulos, habría
/// que traducirlos aquí y volveríamos a tener textos que cambian de idioma según quién los
/// escribió. El cliente cruza la clave con lo que ya sabe pintar.
/// </summary>
public record GetTemplateUsagesQuery(Guid TenantId) : IRequest<Result<List<TemplateUsageDto>>>;
