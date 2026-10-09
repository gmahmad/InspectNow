using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InspectNow.Api.Diagnostics;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Expected business outcomes are handled by the use case/controller.
        // Unexpected exceptions stay 500; do not disguise programming bugs as bad requests.
        logger.LogError(exception, "Unhandled API exception. TraceId: {TraceId}",
            ApiProblemDetails.TraceId(httpContext));

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "Please try again. If the problem continues, provide the traceId to support."
        };
        ApiProblemDetails.Customize(httpContext, problem);
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext, ProblemDetails = problem
        });
        if (!written)
        {
            // Still return a safe JSON error if Accept is not supported by the writer.
            await httpContext.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions?)null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        }
        return true;
    }
}
