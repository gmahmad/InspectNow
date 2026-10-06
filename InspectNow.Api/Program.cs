using InspectNow.Application.Inspections;
using InspectNow.Application.Templates;
using InspectNow.Infrastructure.Persistence;
using InspectNow.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("InspectNow");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'InspectNow' is missing.");
}

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

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}
