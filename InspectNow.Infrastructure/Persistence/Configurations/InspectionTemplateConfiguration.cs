using InspectNow.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InspectNow.Infrastructure.Persistence.Configurations;

public sealed class InspectionTemplateConfiguration
    : IEntityTypeConfiguration<InspectionTemplate>
{
    public void Configure(
        EntityTypeBuilder<InspectionTemplate> builder)
    {
        builder.ToTable("InspectionTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasMaxLength(InspectionTemplate.MaxNameLength)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasMany(t => t.Questions)
            .WithOne()
            .HasForeignKey("InspectionTemplateId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.Questions)
            .HasField("_questions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(t => t.Version)
            .IsConcurrencyToken()
            .ValueGeneratedNever();
    }
}