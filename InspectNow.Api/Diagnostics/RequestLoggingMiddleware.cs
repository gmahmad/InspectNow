using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace InspectNow.Api.Diagnostics;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var traceId = ApiProblemDetails.TraceId(context);
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = traceId;
            return Task.CompletedTask;
        });
        try
        {
            await next(context);
        }
        finally
        {
            // Route pattern avoids logging site names, raw URLs, headers or bodies.
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
            logger.LogInformation(
                "HTTP {Method} {Route} returned {StatusCode} in {ElapsedMs} ms. Aborted: {Aborted}",
                context.Request.Method, route, context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                context.RequestAborted.IsCancellationRequested);
        }
    }
}
