using InspectNow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InspectNow.Api.Health;

public sealed class DatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
        var connected = await db.Database.CanConnectAsync(cancellationToken);
        // Connectivity only; migrations run as an explicit deployment step.
        return connected ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
    }
}
