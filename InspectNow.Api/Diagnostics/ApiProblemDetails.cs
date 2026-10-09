using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InspectNow.Api.Diagnostics;

public static class ApiProblemDetails
{
    public static string TraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    public static void Customize(HttpContext context, ProblemDetails problem)
    {
        problem.Extensions["traceId"] = TraceId(context);
        // No query string: filters may contain customer information.
        problem.Instance ??= context.Request.Path.Value;
    }
}
