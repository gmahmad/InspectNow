using InspectNow.Domain.Inspections;
using InspectNow.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InspectNow.Infrastructure.Persistence.Configurations;

public sealed class InspectionQuestionConfiguration
    : IEntityTypeConfiguration<InspectionQuestion>
{
    public void Configure(
        EntityTypeBuilder<InspectionQuestion> builder)
    {
        builder.ToTable("InspectionQuestions");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.Text)
            .HasMaxLength(TemplateQuestion.MaxTextLength)
            .IsRequired();

        builder.HasIndex("InspectionId", nameof(InspectionQuestion.Position))
            .IsUnique();

        builder.HasIndex(
                "InspectionId",
                nameof(InspectionQuestion.SourceQuestionId))
            .IsUnique();
    }
}