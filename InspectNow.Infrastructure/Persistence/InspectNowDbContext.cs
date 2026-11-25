using InspectNow.Domain.Inspections;
using InspectNow.Domain.Organizations;
using InspectNow.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace InspectNow.Infrastructure.Persistence;

public sealed class InspectNowDbContext : DbContext
{
    private readonly OrganizationScope? _organization;
    private long? SelectedOrganizationId => _organization?.Id;

    public InspectNowDbContext(
        DbContextOptions<InspectNowDbContext> options,
        OrganizationScope? organization = null)
        : base(options)
    {
        _organization = organization;
    }

    public DbSet<InspectionTemplate> InspectionTemplates =>
        Set<InspectionTemplate>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(InspectNowDbContext).Assembly);

        modelBuilder.Entity<InspectionTemplate>()
            .HasQueryFilter(template => template.OrganizationId == SelectedOrganizationId);
        modelBuilder.Entity<Inspection>()
            .HasQueryFilter(inspection => inspection.OrganizationId == SelectedOrganizationId);
    }

    public void AddTemplate(InspectionTemplate template)
    {
        Entry(template).Property(item => item.OrganizationId).CurrentValue = RequireOrganization();
        InspectionTemplates.Add(template);
    }

    public void AddInspection(Inspection inspection)
    {
        Entry(inspection).Property(item => item.OrganizationId).CurrentValue = RequireOrganization();
        Inspections.Add(inspection);
    }

    private long RequireOrganization() => SelectedOrganizationId
        ?? throw new InvalidOperationException("An organization is required to create inspection records.");
}
