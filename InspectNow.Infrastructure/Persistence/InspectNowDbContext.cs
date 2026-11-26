using InspectNow.Domain.Inspections;
using InspectNow.Domain.Organizations;
using InspectNow.Domain.Forms;
using InspectNow.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace InspectNow.Infrastructure.Persistence;

public sealed class InspectNowDbContext : IdentityDbContext<IdentityUser>
{
    private readonly OrganizationScope? _organization;
    private long? SelectedOrganizationId => _organization?.Id;
    private bool InspectorOnly => _organization?.Role == WorkspaceRole.Inspector;
    private string? AssignedUser => _organization?.IsLocal == true ? _organization.Subject : null;

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
    public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();
    public DbSet<CorrectiveAction> CorrectiveActions => Set<CorrectiveAction>();
    public DbSet<FormDefinition> Forms => Set<FormDefinition>();
    public DbSet<FormResponse> FormResponses => Set<FormResponse>();

    public async Task<OrganizationMembership?> LockMembership(long organizationId, string issuer, string subject,
        CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("A transaction is required to lock a membership.");
        var matches = await OrganizationMemberships.FromSqlInterpolated(
            $"SELECT * FROM \"OrganizationMemberships\" WHERE \"OrganizationId\" = {organizationId} AND \"Issuer\" = {issuer} AND \"Subject\" = {subject} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return matches.SingleOrDefault();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(InspectNowDbContext).Assembly);

        modelBuilder.Entity<FormDefinition>().HasQueryFilter(form => form.OrganizationId == SelectedOrganizationId);
        modelBuilder.Entity<FormResponse>().HasQueryFilter(response => response.OrganizationId == SelectedOrganizationId);
        modelBuilder.Entity<OrganizationRole>(role =>
        {
            role.Property(item => item.Name).HasMaxLength(80);
            role.HasIndex(item => new { item.OrganizationId, item.NormalizedName }).IsUnique();
            role.HasOne<Organization>().WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            role.HasQueryFilter(item => item.OrganizationId == SelectedOrganizationId);
        });

        modelBuilder.Entity<InspectionTemplate>()
            .HasQueryFilter(template => template.OrganizationId == SelectedOrganizationId);
        modelBuilder.Entity<Inspection>()
            .HasQueryFilter(inspection => inspection.OrganizationId == SelectedOrganizationId &&
                (!InspectorOnly || AssignedUser != null && inspection.AssignedToUserId == AssignedUser));

        modelBuilder.Entity<CorrectiveAction>().HasQueryFilter(action =>
            action.OrganizationId == SelectedOrganizationId &&
            (!InspectorOnly || AssignedUser != null && action.AssigneeId == AssignedUser));

        modelBuilder.Entity<Site>(site =>
        {
            site.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            site.Property(item => item.Name).HasMaxLength(200);
            site.HasOne<Organization>().WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            site.HasQueryFilter(item => item.OrganizationId == SelectedOrganizationId);
        });
        modelBuilder.Entity<WorkspaceInvitation>(invitation =>
        {
            invitation.HasIndex(item => item.CodeHash).IsUnique();
            invitation.Property(item => item.CodeHash).HasMaxLength(64);
            invitation.Property(item => item.RedeemedAtUtc).IsConcurrencyToken();
            invitation.HasOne<Organization>().WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });
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
