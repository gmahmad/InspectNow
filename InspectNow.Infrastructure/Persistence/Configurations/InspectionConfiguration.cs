using InspectNow.Domain.Inspections;
using InspectNow.Domain.Templates;
using InspectNow.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InspectNow.Infrastructure.Persistence.Configurations;

public sealed class InspectionConfiguration
    : IEntityTypeConfiguration<Inspection>
{
    public void Configure(EntityTypeBuilder<Inspection> builder)
    {
        builder.ToTable("Inspections");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.AssignedToUserId).HasMaxLength(255);
        builder.HasIndex(i => new { i.OrganizationId, i.AssignedToUserId });
        builder.HasOne<Site>().WithMany().HasForeignKey(i => new { i.OrganizationId, i.SiteId })
            .HasPrincipalKey(site => new { site.OrganizationId, site.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Property<long>("OrganizationId");
        builder.HasOne<Organization>().WithMany().HasForeignKey("OrganizationId")
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(i => i.Id).UseIdentityByDefaultColumn();

        builder.Property(i => i.SiteName)
            .HasMaxLength(Inspection.MaxSiteNameLength)
            .IsRequired();

        builder.Property(i => i.TemplateName)
            .HasMaxLength(InspectionTemplate.MaxNameLength)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.StartedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(i => i.SubmittedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(i => i.Version)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.HasOne<InspectionTemplate>()
            .WithMany()
            .HasForeignKey("OrganizationId", nameof(Inspection.TemplateId))
            .HasPrincipalKey("OrganizationId", nameof(InspectionTemplate.Id))
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Questions)
            .WithOne()
            .HasForeignKey("InspectionId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Questions)
            .HasField("_questions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
