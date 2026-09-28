using BuildingBlocks.Application.Abstractions;
using Tags.Application.DTOs;

namespace Tags.Application.Queries;

public sealed record GetTagsQuery(Guid TenantId) : IQuery<List<TagDto>>;
