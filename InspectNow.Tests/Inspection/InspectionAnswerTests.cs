using InspectNow.Domain.Inspections;
using InspectNow.Domain.Templates;
using Xunit;
using InspectNow.Tests.Support;

namespace InspectNow.Tests.Inspections;

public class InspectionAnswerTests
{
    [Fact]
    public void NewInspection_QuestionIsUnanswered()
    {
        var inspection = CreateInspection();
        var question = Assert.Single(inspection.Questions);

        Assert.Null(question.Answer);
        Assert.Null(question.Comment);
    }

    [Fact]
    public void AnswerQuestion_WithValidAnswer_UpdatesAnswerAndVersion()
    {
        var inspection = CreateInspection();
        var question = Assert.Single(inspection.Questions);
        var originalVersion = inspection.Version;

        inspection.AnswerQuestion(
            question.Id,
            InspectionAnswer.Fail,
            "  Floor needs cleaning.  ");

        Assert.Equal(InspectionAnswer.Fail, question.Answer);
        Assert.Equal("Floor needs cleaning.", question.Comment);
        Assert.NotEqual(originalVersion, inspection.Version);
    }

    [Fact]
    public void AnswerQuestion_RequiredQuestionCannotBeNotApplicable()
    {
        var inspection = CreateInspection(isRequired: true);
        var question = Assert.Single(inspection.Questions);
        var originalVersion = inspection.Version;

        Assert.Throws<InvalidOperationException>(() =>
            inspection.AnswerQuestion(
                question.Id,
                InspectionAnswer.NotApplicable,
                null));

        Assert.Null(question.Answer);
        Assert.Equal(originalVersion, inspection.Version);
    }

    [Fact]
    public void AnswerQuestion_OptionalQuestionCanBeNotApplicable()
    {
        var inspection = CreateInspection(isRequired: false);
        var question = Assert.Single(inspection.Questions);

        inspection.AnswerQuestion(
            question.Id,
            InspectionAnswer.NotApplicable,
            "No carpet at this site.");

        Assert.Equal(
            InspectionAnswer.NotApplicable,
            question.Answer);
    }

    [Fact]
    public void AnswerQuestion_FromAnotherInspection_Throws()
    {
        var inspection = CreateInspection();
        var otherInspection = CreateInspection(questionId: 302);
        var otherQuestion = Assert.Single(otherInspection.Questions);

        Assert.Throws<ArgumentException>(() =>
            inspection.AnswerQuestion(
                otherQuestion.Id,
                InspectionAnswer.Pass,
                null));
    }

    [Fact]
    public void AnswerQuestion_InvalidEnum_Throws()
    {
        var inspection = CreateInspection();
        var question = Assert.Single(inspection.Questions);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            inspection.AnswerQuestion(
                question.Id,
                (InspectionAnswer)99,
                null));
    }

    [Fact]
    public void AnswerQuestion_InvalidComment_PreservesExistingAnswer()
    {
        var inspection = CreateInspection();
        var question = Assert.Single(inspection.Questions);

        inspection.AnswerQuestion(
            question.Id,
            InspectionAnswer.Pass,
            "Checked.");

        var originalVersion = inspection.Version;

        Assert.Throws<ArgumentException>(() =>
            inspection.AnswerQuestion(
                question.Id,
                InspectionAnswer.Fail,
                new string(
                    'x',
                    InspectionQuestion.MaxCommentLength + 1)));

        Assert.Equal(InspectionAnswer.Pass, question.Answer);
        Assert.Equal("Checked.", question.Comment);
        Assert.Equal(originalVersion, inspection.Version);
    }

    [Fact]
    public void AnswerQuestion_Again_ReplacesPreviousAnswer()
    {
        var inspection = CreateInspection();
        var question = Assert.Single(inspection.Questions);

        inspection.AnswerQuestion(
            question.Id,
            InspectionAnswer.Fail,
            "Needs cleaning.");

        inspection.AnswerQuestion(
            question.Id,
            InspectionAnswer.Pass,
            "   ");

        Assert.Equal(InspectionAnswer.Pass, question.Answer);
        Assert.Null(question.Comment);
        Assert.Single(inspection.Questions);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void AnswerQuestion_NonPositiveId_Throws(long questionId)
    {
        var inspection = CreateInspection();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            inspection.AnswerQuestion(questionId, InspectionAnswer.Pass, null));
    }

    private static Inspection CreateInspection(
        bool isRequired = true,
        long questionId = 301)
    {
        var template = new InspectionTemplate(
            "Office Cleaning Inspection");

        template.AddQuestion(
            "Are the floors clean?",
            isRequired);

        template.Publish();
        PersistedEntityFixture.AssignId(template, 101);
        PersistedEntityFixture.AssignId(Assert.Single(template.Questions), 201);

        var inspection = Inspection.Start(
            template,
            "Lahore Office",
            new DateTimeOffset(
                2026, 10, 6, 7, 0, 0,
                TimeSpan.Zero));

        PersistedEntityFixture.AssignId(inspection, 401);
        PersistedEntityFixture.AssignId(Assert.Single(inspection.Questions), questionId);
        return inspection;
    }
}