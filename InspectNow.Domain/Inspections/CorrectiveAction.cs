namespace InspectNow.Domain.Inspections;

public enum CorrectiveActionStatus { Open, ReadyForReview, Closed }

public sealed class CorrectiveAction
{
    public long Id { get; private set; }
    public long OrganizationId { get; private set; }
    public long InspectionId { get; private set; }
    public long QuestionId { get; private set; }
    public string SiteName { get; private set; } = "";
    public string Finding { get; private set; } = "";
    public string Instructions { get; private set; } = "";
    public string AssigneeId { get; private set; } = "";
    public DateOnly DueDate { get; private set; }
    public CorrectiveActionStatus Status { get; private set; }
    public string? Resolution { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();

    private CorrectiveAction() { }

    public static CorrectiveAction Create(Inspection inspection, long questionId, string instructions,
        string assigneeId, DateOnly dueDate, DateTimeOffset now)
    {
        var question = inspection.Questions.SingleOrDefault(item => item.Id == questionId);
        if (inspection.Status != InspectionStatus.Submitted || question?.Answer != InspectionAnswer.Fail)
            throw new InvalidOperationException("Corrective actions require a failed answer on a submitted inspection.");
        return new CorrectiveAction
        {
            OrganizationId = inspection.OrganizationId, InspectionId = inspection.Id, QuestionId = questionId,
            SiteName = inspection.SiteName, Finding = question.Text, Instructions = instructions,
            AssigneeId = assigneeId, DueDate = dueDate, CreatedAtUtc = now
        };
    }

    public void Resolve(string resolution)
    {
        if (Status != CorrectiveActionStatus.Open)
            throw new InvalidOperationException("Only open actions can be sent for review.");
        Resolution = resolution;
        Status = CorrectiveActionStatus.ReadyForReview;
        Version = Guid.NewGuid();
    }

    public void Review(bool approve, string? note, DateTimeOffset now)
    {
        if (Status != CorrectiveActionStatus.ReadyForReview)
            throw new InvalidOperationException("Only actions awaiting review can be reviewed.");
        ReviewNote = note;
        Status = approve ? CorrectiveActionStatus.Closed : CorrectiveActionStatus.Open;
        ClosedAtUtc = approve ? now : null;
        Version = Guid.NewGuid();
    }
}
