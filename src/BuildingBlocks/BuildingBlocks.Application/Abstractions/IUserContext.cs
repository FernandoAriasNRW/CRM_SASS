namespace BuildingBlocks.Application.Abstractions;

public interface IUserContext
{
    Guid UserId { get; }
    Guid TenantId { get; }
    string Role { get; }
}
