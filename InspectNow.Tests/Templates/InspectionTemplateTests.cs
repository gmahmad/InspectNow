using InspectNow.Domain.Templates;
using Xunit;

namespace InspectNow.Tests.Templates;

public class InspectionTemplateTests
{
    [Fact]
    public void Constructor_WithValidName_CreatesDraftWithTrimmedName()
    {
        var template = new InspectionTemplate("  Office Cleaning  ");

        Assert.Equal(0L, template.Id); // Assigned by PostgreSQL on save.
        Assert.Equal("Office Cleaning", template.Name);
        Assert.Equal(TemplateStatus.Draft, template.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string? name)
    {
        Assert.Throws<ArgumentException>(
            () => new InspectionTemplate(name!));
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        var template = new InspectionTemplate("Original Name");

        template.Rename("  Updated Name  ");

        Assert.Equal("Updated Name", template.Name);
    }

    [Fact]
    public void Rename_WithNameExceedingLimit_PreservesExistingName()
    {
        var template = new InspectionTemplate("Original Name");
        string invalidName =
            new string('A', InspectionTemplate.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(
            () => template.Rename(invalidName));

        Assert.Equal("Original Name", template.Name);
    }

    [Fact]
    public void AddQuestion_ToDraft_AddsTrimmedQuestion()
    {
        var template = new InspectionTemplate("Office Cleaning");

        var addedQuestion = template.AddQuestion(
            "  Describe the floor condition.  ",
            isRequired: true);

        var question = Assert.Single(template.Questions);

        Assert.Same(addedQuestion, question);
        Assert.Equal(0L, question.Id);
        Assert.Equal("Describe the floor condition.", question.Text);
        Assert.True(question.IsRequired);
    }

    [Fact]
    public void AddQuestion_WithBlankText_DoesNotAddQuestion()
    {
        var template = new InspectionTemplate("Office Cleaning");

        Assert.Throws<ArgumentException>(
            () => template.AddQuestion("   ", isRequired: true));

        Assert.Empty(template.Questions);
    }

    [Fact]
    public void Publish_WithoutQuestions_ThrowsAndRemainsDraft()
    {
        var template = new InspectionTemplate("Office Cleaning");

        Assert.Throws<InvalidOperationException>(
            () => template.Publish());

        Assert.Equal(TemplateStatus.Draft, template.Status);
    }

    [Fact]
    public void Publish_WithQuestion_ChangesStatusToPublished()
    {
        var template = new InspectionTemplate("Office Cleaning");
        template.AddQuestion("Describe the floor condition.", true);

        template.Publish();

        Assert.Equal(TemplateStatus.Published, template.Status);
    }

    [Fact]
    public void Rename_AfterPublishing_ThrowsAndPreservesName()
    {
        var template = new InspectionTemplate("Office Cleaning");
        template.AddQuestion("Describe the floor condition.", true);
        template.Publish();

        Assert.Throws<InvalidOperationException>(
            () => template.Rename("Changed Name"));

        Assert.Equal("Office Cleaning", template.Name);
    }

    [Fact]
    public void AddQuestion_AfterPublishing_ThrowsAndPreservesQuestions()
    {
        var template = new InspectionTemplate("Office Cleaning");
        template.AddQuestion("Original question", true);
        template.Publish();

        Assert.Throws<InvalidOperationException>(
            () => template.AddQuestion("Another question", false));

        var question = Assert.Single(template.Questions);
        Assert.Equal("Original question", question.Text);
    }

    [Fact]
    public void Publish_AlreadyPublished_Throws()
    {
        var template = new InspectionTemplate("Office Cleaning");
        template.AddQuestion("Describe the floor condition.", true);
        template.Publish();

        Assert.Throws<InvalidOperationException>(
            () => template.Publish());

        Assert.Equal(TemplateStatus.Published, template.Status);
    }

}