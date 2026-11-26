using InspectNow.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InspectNow.Infrastructure.Persistence.Configurations;

public sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("OrganizationMemberships");
        builder.HasKey(membership => new { membership.OrganizationId, membership.Issuer, membership.Subject });
        builder.HasIndex(membership => new { membership.Issuer, membership.Subject });
        builder.Property(membership => membership.AllowedModules).HasDefaultValue(WorkspaceModules.All)
            .HasSentinel((WorkspaceModules)(-1)).IsConcurrencyToken();
        builder.Property(membership => membership.Role).IsConcurrencyToken();
        builder.Property(membership => membership.Issuer)
            .HasMaxLength(OrganizationMembership.MaxIdentityPartLength);
        builder.Property(membership => membership.Subject)
            .HasMaxLength(OrganizationMembership.MaxIdentityPartLength);
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(membership => membership.OrganizationId);
    }
}
