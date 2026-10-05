using InspectNow.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace InspectNow.Infrastructure.Persistence;

public sealed class InspectNowDbContext : DbContext
{
    public InspectNowDbContext(
        DbContextOptions<InspectNowDbContext> options)
        : base(options)
    {
    }

    public DbSet<InspectionTemplate> InspectionTemplates =>
        Set<InspectionTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(InspectNowDbContext).Assembly);
    }
}