using InspectNow.Domain.Forms;
using InspectNow.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InspectNow.Infrastructure.Persistence.Configurations;

public sealed class FormConfiguration : IEntityTypeConfiguration<FormDefinition>
{
    public void Configure(EntityTypeBuilder<FormDefinition> builder)
    {
        builder.HasAlternateKey(form => new { form.OrganizationId, form.Id });
        builder.HasOne<Organization>().WithMany().HasForeignKey(form => form.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(form => form.Name).HasMaxLength(200);
        builder.Property(form => form.Kind).HasMaxLength(30);
        builder.Property(form => form.FieldsJson).HasColumnType("jsonb");
        builder.Property(form => form.Version).IsConcurrencyToken();
        builder.HasIndex(form => form.PublicKey).IsUnique();
    }
}

public sealed class FormResponseConfiguration : IEntityTypeConfiguration<FormResponse>
{
    public void Configure(EntityTypeBuilder<FormResponse> builder)
    {
        builder.HasOne<FormDefinition>().WithMany()
            .HasForeignKey(response => new { response.OrganizationId, response.FormId })
            .HasPrincipalKey(form => new { form.OrganizationId, form.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(response => response.Issuer).HasMaxLength(255);
        builder.Property(response => response.Subject).HasMaxLength(255);
        builder.Property(response => response.AnswersJson).HasColumnType("jsonb");
        builder.Property(response => response.Version).IsConcurrencyToken();
        builder.HasIndex(response => new { response.OrganizationId, response.FormId, response.Issuer, response.Subject })
            .IsUnique().HasFilter("\"SubmittedAtUtc\" IS NULL");
        builder.HasIndex(response => new { response.OrganizationId, response.FormId, response.SubmittedAtUtc });
    }
}
