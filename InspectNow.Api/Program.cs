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
using InspectNow.Api.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;

// --migrate runs an explicit one-shot database job without starting HTTP.
var migrateOnly = args.Contains("--migrate", StringComparer.Ordinal);
var seedBusinessDemo = args.Contains("--seed-business-demo", StringComparer.Ordinal);
var hostArgs = args.Where(arg => arg is not ("--migrate" or "--seed-business-demo")).ToArray();
var builder = WebApplication.CreateBuilder(hostArgs);
var connectionString = DatabaseConnectionString.Resolve(builder.Configuration);

// Add services to the container.

builder.Services.AddDbContext<InspectNowDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<OrganizationScope>();
builder.Services.AddSingleton<PlatformOwner>();
builder.Services.AddScoped<IAuthorizationHandler, OrganizationAccessHandler>();
builder.Services.AddIdentityCore<IdentityUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
}).AddEntityFrameworkStores<InspectNowDbContext>().AddSignInManager();
var authentication = builder.Services.AddAuthentication("InspectNow")
    .AddPolicyScheme("InspectNow", null, options =>
    {
        options.ForwardDefaultSelector = context => context.Request.Headers.ContainsKey("Authorization")
            ? JwtBearerDefaults.AuthenticationScheme : IdentityConstants.ApplicationScheme;
    })
    .AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Authentication:Authority"];
    options.Audience = builder.Configuration["Authentication:Audience"];
    options.MapInboundClaims = false;
});
authentication.AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-Token";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1)
        }));
    options.AddPolicy("public-form", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1)
        }));
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("LocalAccount", policy => policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme).RequireAuthenticatedUser())
    .AddPolicy(OrganizationAccess.Policy, policy =>
        policy.RequireAuthenticatedUser().AddRequirements(new OrganizationAccess()));

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

if (seedBusinessDemo)
{
    if (migrateOnly) throw new InvalidOperationException("Run migrations before seeding the business demo.");
    await BusinessDemoSeeder.Seed(app.Services, app.Configuration, app.Environment);
    app.Logger.LogInformation("Business demo workspaces are ready.");
    return;
}

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

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var request = context.Request;
    if (request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(request.Method) &&
        !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method) &&
        (request.Path.StartsWithSegments("/api/account") || !request.Headers.ContainsKey("Authorization")))
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
    }
    await next(context);
});

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
