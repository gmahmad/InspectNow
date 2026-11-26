using InspectNow.Domain.Inspections;
using InspectNow.Domain.Organizations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InspectNow.Infrastructure.Persistence.Configurations;

public sealed class CorrectiveActionConfiguration : IEntityTypeConfiguration<CorrectiveAction>
{
    public void Configure(EntityTypeBuilder<CorrectiveAction> builder)
    {
        builder.HasIndex(action => action.QuestionId).IsUnique();
        builder.HasIndex(action => new { action.OrganizationId, action.AssigneeId, action.DueDate });
        builder.HasOne<Organization>().WithMany().HasForeignKey(action => action.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionQuestion>().WithMany().HasForeignKey(action => action.QuestionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IdentityUser>().WithMany().HasForeignKey(action => action.AssigneeId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(action => action.SiteName).HasMaxLength(200);
        builder.Property(action => action.Finding).HasMaxLength(500);
        builder.Property(action => action.AssigneeId).HasMaxLength(255);
        builder.Property(action => action.Instructions).HasMaxLength(2000);
        builder.Property(action => action.Resolution).HasMaxLength(2000);
        builder.Property(action => action.ReviewNote).HasMaxLength(2000);
        builder.Property(action => action.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(action => action.Version).IsConcurrencyToken().ValueGeneratedNever();
    }
}
