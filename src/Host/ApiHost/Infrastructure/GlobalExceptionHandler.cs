using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApiHost.Infrastructure;

/// <summary>
/// Traduce cualquier excepción no controlada a una respuesta RFC 7807 (ProblemDetails).
///
/// Garantiza dos cosas:
///  1. Ninguna excepción escapa como stack trace al cliente en producción.
///  2. Todos los errores de la API tienen la misma forma, para que el frontend
///     pueda tratarlos de manera uniforme en el error.interceptor.
///
/// El detalle real siempre se registra en el log con el traceId, de modo que un
/// error 500 en producción sigue siendo diagnosticable sin filtrar nada.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        ProblemDetails problem = exception switch
        {
            ValidationException validationException => BuildValidationProblem(validationException),

            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Acceso denegado",
                Detail = "No tiene permisos para realizar esta operación."
            },

            KeyNotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Recurso no encontrado",
                Detail = "El recurso solicitado no existe."
            },

            // Reglas de negocio violadas desde el dominio.
            InvalidOperationException invalidOperation => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Operación no válida",
                Detail = invalidOperation.Message
            },

            ArgumentException argumentException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Petición incorrecta",
                Detail = argumentException.Message
            },

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno del servidor",
                // En desarrollo mostramos el mensaje real; en producción, nunca.
                Detail = environment.IsDevelopment()
                    ? exception.ToString()
                    : "Ocurrió un error inesperado. Contacte a soporte con el identificador de traza."
            }
        };

        problem.Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}";
        problem.Extensions["traceId"] = traceId;

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Excepción no controlada en {Method} {Path}. TraceId: {TraceId}",
                httpContext.Request.Method, httpContext.Request.Path, traceId);
        }
        else
        {
            logger.LogWarning(
                "Petición rechazada ({Status}) en {Method} {Path}. TraceId: {TraceId}. Motivo: {Reason}",
                problem.Status, httpContext.Request.Method, httpContext.Request.Path, traceId, exception.Message);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    private static ProblemDetails BuildValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Error de validación",
            Detail = "Uno o más campos de la petición no son válidos."
        };
    }
}
