using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Queries;

/// <summary>Cuántas veces se ha usado una plantilla y cuándo fue la última.</summary>
public record TemplateUsageDto(string Key, int Count, DateTime LastUsedAtUtc);
