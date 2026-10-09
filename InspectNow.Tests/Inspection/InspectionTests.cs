using InspectNow.Domain.Inspections;
using InspectNow.Domain.Templates;
using Xunit;
using InspectNow.Tests.Support;

namespace InspectNow.Tests.Inspections;

public sealed class InspectionTests
{
    [Fact]
    public void Start_WithPublishedTemplate_CreatesDraftWithSnapshots()
    {
        var template = new InspectionTemplate(
            "Office Cleaning Inspection");

        var sourceQuestion = template.AddQuestion(
            "Are the floors clean?",
            true);

        template.Publish();
        PersistedEntityFixture.AssignId(template, 101);
        PersistedEntityFixture.AssignId(Assert.Single(template.Questions), 201);

        var startedAt = new DateTimeOffset(
            2026, 10, 6, 10, 0, 0,
            TimeSpan.FromHours(5));

        var inspection = Inspection.Start(
            template,
            "  Lahore Office  ",
            startedAt);

        Assert.Equal(0L, inspection.Id);
        Assert.NotEqual(Guid.Empty, inspection.Version);
        Assert.Equal(template.Id, inspection.TemplateId);
        Assert.Equal(template.Version, inspection.TemplateVersion);
        Assert.Equal(template.Name, inspection.TemplateName);
        Assert.Equal("Lahore Office", inspection.SiteName);
        Assert.Equal(InspectionStatus.Draft, inspection.Status);

        Assert.Equal(
            startedAt.ToUniversalTime(),
            inspection.StartedAtUtc);

        Assert.Equal(
            TimeSpan.Zero,
            inspection.StartedAtUtc.Offset);

        var question = Assert.Single(inspection.Questions);

        Assert.Equal(0L, question.Id);
        Assert.NotSame(sourceQuestion, question);
        Assert.Equal(sourceQuestion.Id, question.SourceQuestionId);
        Assert.Equal("Are the floors clean?", question.Text);
        Assert.True(question.IsRequired);
        Assert.Equal(1, question.Position);
    }

    [Fact]
    public void Start_WithDraftTemplate_Throws()
    {
        var template = new InspectionTemplate("Draft Template");
        template.AddQuestion("Are the floors clean?", true);

        Assert.Throws<InvalidOperationException>(() =>
            Inspection.Start(
                template,
                "Lahore Office",
                DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_WithBlankSiteName_Throws(string siteName)
    {
        var template = CreatePublishedTemplate();

        Assert.Throws<ArgumentException>(() =>
            Inspection.Start(
                template,
                siteName,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Start_WithSiteNameTooLong_Throws()
    {
        var template = CreatePublishedTemplate();

        var siteName = new string(
            'a',
            Inspection.MaxSiteNameLength + 1);

        Assert.Throws<ArgumentException>(() =>
            Inspection.Start(
                template,
                siteName,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Start_Twice_CreatesIndependentQuestionSnapshots()
    {
        var template = CreatePublishedTemplate();
        var startedAt = DateTimeOffset.UtcNow;

        var first = Inspection.Start(
            template,
            "Lahore Office",
            startedAt);

        var second = Inspection.Start(
            template,
            "Islamabad Office",
            startedAt);

        Assert.NotSame(first, second);
        Assert.Equal(0L, first.Id);
        Assert.Equal(0L, second.Id);

        var firstQuestion = Assert.Single(first.Questions);
        var secondQuestion = Assert.Single(second.Questions);

        Assert.NotSame(firstQuestion, secondQuestion);

        Assert.Equal(
            firstQuestion.SourceQuestionId,
            secondQuestion.SourceQuestionId);
    }

    [Fact]
    public void Start_WithUnsavedPublishedTemplate_Throws()
    {
        var template = new InspectionTemplate("Unsaved template");
        template.AddQuestion("Question", true);
        template.Publish();

        Assert.Throws<InvalidOperationException>(() =>
            Inspection.Start(template, "Office", DateTimeOffset.UtcNow));
    }

    private static InspectionTemplate CreatePublishedTemplate()
    {
        var template = new InspectionTemplate(
            "Office Cleaning Inspection");

        template.AddQuestion("Are the floors clean?", true);
        template.Publish();
        PersistedEntityFixture.AssignId(template, 101);
        PersistedEntityFixture.AssignId(Assert.Single(template.Questions), 201);

        return template;
    }

    [Fact]
    public void Start_NormalizesSubMicrosecondPrecision()
    {
        var template = CreatePublishedTemplate();
        var startedAt = new DateTimeOffset(2026, 10, 9, 16, 40, 22,
            TimeSpan.FromHours(5)).AddTicks(8_474_303);
        var inspection = Inspection.Start(template, "Office", startedAt);
        Assert.Equal(startedAt.ToUniversalTime().AddTicks(-3), inspection.StartedAtUtc);
    }
}
