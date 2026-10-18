using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PS.Wallet.Application;
using PS.Wallet.Infrastructure.Persistence;

namespace PS.Wallet.API.Middleware;

public sealed class ExceptionHandlingMiddleware : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationAppException validation => CreateValidationProblem(validation),
            ConflictException conflict => CreateConflictProblem(conflict),
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found",
                Detail = notFound.Message,
                Extensions = { ["code"] = notFound.Code },
            },
            InvalidCursorException invalidCursor => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid pagination cursor",
                Detail = invalidCursor.Message,
                Extensions = { ["code"] = "INVALID_CURSOR" },
            },
            UncertainCommitException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service unavailable",
                Detail = "The operation outcome could not be confirmed. Retry with the same idempotency key.",
                Extensions = { ["code"] = "IDEMPOTENCY_IN_PROGRESS" },
            },
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static ProblemDetails CreateValidationProblem(ValidationAppException validation)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Request validation failed",
            Detail = "One or more fields contain invalid values.",
            Extensions = { ["code"] = "VALIDATION_ERROR" },
        };

        problem.Extensions["errors"] = validation.Errors;
        return problem;
    }

    private static ProblemDetails CreateConflictProblem(ConflictException conflict)
    {
        var isInProgress = conflict.Code == "IDEMPOTENCY_IN_PROGRESS";

        return new ProblemDetails
        {
            Status = isInProgress ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status409Conflict,
            Title = isInProgress ? "Service unavailable" : "Request conflict",
            Detail = conflict.Message,
            Extensions = { ["code"] = conflict.Code },
        };
    }
}
