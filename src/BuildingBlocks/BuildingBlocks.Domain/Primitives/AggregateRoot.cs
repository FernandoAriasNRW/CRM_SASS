namespace BuildingBlocks.Domain.Primitives;

public abstract class AggregateRoot : Entity
{
    protected AggregateRoot(Guid id) 
    {
        Id = id;
    }

    // Constructor necesario para persistencia/EF Core
    protected AggregateRoot() : base() { }
}