using InspectNow.Domain.Inspections;
using InspectNow.Tests.Support;

namespace InspectNow.Tests.Inspections;

public sealed class InspectionSubmissionTests
{
    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 10, 8, 17, 0, 0, TimeSpan.FromHours(5));

    [Fact]
    public void Submit_MissingRequiredAnswer_PreservesDraftAndVersion()
    {
        var inspection = InspectionFixture.Create();
        var version = inspection.Version;
        Assert.Throws<InvalidOperationException>(() => inspection.Submit(SubmittedAt));
        Assert.Equal(InspectionStatus.Draft, inspection.Status);
        Assert.Null(inspection.SubmittedAtUtc);
        Assert.Equal(version, inspection.Version);
    }

    [Theory]
    [InlineData(InspectionAnswer.Pass)]
    [InlineData(InspectionAnswer.Fail)]
    public void Submit_RequiredAnswered_AllowsUnansweredOptionalAndNormalizesUtc(InspectionAnswer answer)
    {
        var inspection = InspectionFixture.Create();
        inspection.AnswerQuestion(401, answer, null);
        var version = inspection.Version;
        inspection.Submit(SubmittedAt);
        Assert.Equal(InspectionStatus.Submitted, inspection.Status);
        Assert.Equal(SubmittedAt.ToUniversalTime(), inspection.SubmittedAtUtc!.Value);
        Assert.Equal(TimeSpan.Zero, inspection.SubmittedAtUtc.Value.Offset);
        Assert.NotEqual(version, inspection.Version);
        Assert.Null(inspection.Questions[1].Answer);
    }

    [Fact]
    public void Submit_OptionalNotApplicable_IsAllowed()
    {
        var inspection = InspectionFixture.Create();
        inspection.AnswerQuestion(401, InspectionAnswer.Pass, null);
        inspection.AnswerQuestion(402, InspectionAnswer.NotApplicable, "No carpet");
        inspection.Submit(SubmittedAt);
        Assert.Equal(InspectionStatus.Submitted, inspection.Status);
    }

    [Fact]
    public void SubmittedInspection_RejectsAnswerChangesAndResubmission()
    {
        var inspection = InspectionFixture.Create();
        inspection.AnswerQuestion(401, InspectionAnswer.Fail, "Needs cleaning");
        inspection.Submit(SubmittedAt);
        var version = inspection.Version;
        var timestamp = inspection.SubmittedAtUtc;
        Assert.Throws<InvalidOperationException>(() =>
            inspection.AnswerQuestion(401, InspectionAnswer.Pass, null));
        Assert.Throws<InvalidOperationException>(() => inspection.Submit(SubmittedAt.AddHours(1)));
        Assert.Equal(InspectionAnswer.Fail, inspection.Questions[0].Answer);
        Assert.Equal(version, inspection.Version);
        Assert.Equal(timestamp, inspection.SubmittedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(9)]
    public void Submit_NormalizesSubMicrosecondPrecision(int extraTicks)
    {
        var inspection = InspectionFixture.Create();
        inspection.AnswerQuestion(401, InspectionAnswer.Pass, null);
        var expected = SubmittedAt.ToUniversalTime().AddTicks(8_474_300);

        inspection.Submit(SubmittedAt.AddTicks(8_474_300 + extraTicks));

        Assert.Equal(expected, inspection.SubmittedAtUtc!.Value);
        Assert.Equal(0L, inspection.SubmittedAtUtc.Value.Ticks % TimeSpan.TicksPerMicrosecond);
    }
}
