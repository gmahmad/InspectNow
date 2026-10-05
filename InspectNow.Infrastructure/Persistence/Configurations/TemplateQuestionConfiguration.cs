using InspectNow.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InspectNow.Infrastructure.Persistence.Configurations;

public sealed class TemplateQuestionConfiguration
    : IEntityTypeConfiguration<TemplateQuestion>
{
    public void Configure(
        EntityTypeBuilder<TemplateQuestion> builder)
    {
        builder.ToTable("TemplateQuestions");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id)
            .ValueGeneratedNever();

        builder.Property(q => q.Text)
            .HasMaxLength(TemplateQuestion.MaxTextLength)
            .IsRequired();

        builder.Property(q => q.IsRequired)
            .IsRequired();
    }
}