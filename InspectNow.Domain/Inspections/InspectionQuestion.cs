namespace InspectNow.Domain.Inspections;

public sealed class InspectionQuestion
{
    public Guid Id { get; private set; }
    public Guid SourceQuestionId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsRequired { get; private set; }
    public int Position { get; private set; }

    // Used by EF Core when loading saved data.
    private InspectionQuestion()
    {
    }

    internal InspectionQuestion(
        Guid sourceQuestionId,
        string text,
        bool isRequired,
        int position)
    {
        Id = Guid.NewGuid();
        SourceQuestionId = sourceQuestionId;
        Text = text;
        IsRequired = isRequired;
        Position = position;
    }

    public const int MaxCommentLength = 2000;

    public InspectionAnswer? Answer { get; private set; }

    public string? Comment { get; private set; }

    internal void RecordAnswer(
        InspectionAnswer answer,
        string? comment)
    {
        if (!Enum.IsDefined(typeof(InspectionAnswer), answer))
        {
            throw new ArgumentOutOfRangeException(
                nameof(answer),
                "The inspection answer is invalid.");
        }

        if (IsRequired && answer == InspectionAnswer.NotApplicable)
        {
            throw new InvalidOperationException(
                "A required question must be answered with Pass or Fail.");
        }

        string? normalizedComment = string.IsNullOrWhiteSpace(comment)
            ? null
            : comment.Trim();

        if (normalizedComment?.Length > MaxCommentLength)
        {
            throw new ArgumentException(
                $"Comments cannot exceed {MaxCommentLength} characters.",
                nameof(comment));
        }

        Answer = answer;
        Comment = normalizedComment;
    }
}