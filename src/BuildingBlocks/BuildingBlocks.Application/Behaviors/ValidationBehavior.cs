using FluentValidation;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Ejecuta todos los <see cref="IValidator{T}"/> registrados para el request antes de
/// llegar al handler. Si alguno falla, lanza <see cref="ValidationException"/>, que el
/// manejador global de excepciones traduce a un 400 con ValidationProblemDetails.
///
/// Se registra como open behavior en el pipeline de MediatR y debe ir ANTES de
/// AuthorizationBehavior: no tiene sentido autorizar una petición malformada.
///
/// Si un request no tiene validadores registrados, el behavior no hace nada.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
