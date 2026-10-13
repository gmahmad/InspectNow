using InspectNow.Api.Configuration;
using InspectNow.Api.Diagnostics;
using InspectNow.Api.Health;
using InspectNow.Infrastructure.Persistence.Queries;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using InspectNow.Application.Inspections;
using InspectNow.Application.Templates;
using InspectNow.Infrastructure.Persistence;
using InspectNow.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

// --migrate runs an explicit one-shot database job without starting HTTP.
var migrateOnly = args.Contains("--migrate", StringComparer.Ordinal);
var hostArgs = args.Where(arg => arg != "--migrate").ToArray();
var builder = WebApplication.CreateBuilder(hostArgs);
var connectionString = DatabaseConnectionString.Resolve(builder.Configuration);

// Add services to the container.

builder.Services.AddDbContext<InspectNowDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<
    IInspectionTemplateRepository,
    InspectionTemplateRepository>();

builder.Services.AddScoped<CreateInspectionTemplate>();
builder.Services.AddScoped<GetInspectionTemplate>();
builder.Services.AddScoped<AddTemplateQuestion>();
builder.Services.AddScoped<PublishInspectionTemplate>();
builder.Services.AddScoped<ListInspectionTemplates>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<IInspectionRepository, InspectionRepository>();
builder.Services.AddScoped<StartInspection>();
builder.Services.AddScoped<GetInspection>();
builder.Services.AddScoped<SaveInspectionAnswer>();
builder.Services.AddScoped<SubmitInspection>();

builder.Services.AddScoped<IInspectionQueries, InspectionQueries>();
builder.Services.AddScoped<ListInspections>();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        ApiProblemDetails.Customize(context.HttpContext, context.ProblemDetails));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"],
        timeout: TimeSpan.FromSeconds(5));

// Built-in logging providers; JSON console for searchable structured fields.
// Keep the Debug provider so logs also appear in Visual Studio Output.
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

await using var app = builder.Build();

if (migrateOnly)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
    app.Logger.LogInformation("Applying pending InspectNow database migrations.");
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("InspectNow database migrations completed.");
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.UseMiddleware<RequestLoggingMiddleware>();
// Keep the same safe error contract in Development and Production.
// .NET 10 suppresses duplicate diagnostics for exceptions handled by our handler.
app.UseExceptionHandler();
app.UseStatusCodePages();
// Local Compose uses HTTP on loopback; Visual Studio keeps HTTPS redirection.
if (app.Configuration.GetValue("Http:UseHttpsRedirection", true))
    app.UseHttpsRedirection();

app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapControllers();

app.Run();

public partial class Program
{
}
