using BuildingBlocks.Domain;
using MediatR;

namespace BuildingBlocks.Application.Abstractions;

public interface ICommand : IRequest<Result<bool>>;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

// El Handler genérico para comandos con respuesta específica
public interface ICommandHandler<in TCommand, TResponse>
    : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
{ }

// Handler para Comandos estándar (el de tu imagen)
public interface ICommandHandler<in TCommand>
    : IRequestHandler<TCommand, Result<bool>>
    where TCommand : ICommand // <--- Esta restricción resuelve el CS0314
{
}

// Handler para Queries
public interface IQueryHandler<in TQuery, TResponse>
    : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}