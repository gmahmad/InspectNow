using InspectNow.Domain.Inspections;

namespace InspectNow.Application.Inspections;

public sealed class SaveInspectionAnswer
{
    private readonly IInspectionRepository _repository;

    public SaveInspectionAnswer(IInspectionRepository repository)
    {
        _repository = repository;
    }

    public async Task<SaveInspectionAnswerResult> ExecuteAsync(
        long inspectionId, long questionId, InspectionAnswer answer, string? comment,
        Guid expectedVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (expectedVersion == Guid.Empty)
            return new(SaveInspectionAnswerOutcome.InvalidAnswer,
                Error: "The inspection version is required.");

        var inspection = await _repository.GetForUpdateAsync(inspectionId, cancellationToken);
        if (inspection is null)
            return new(SaveInspectionAnswerOutcome.InspectionNotFound);
        if (inspection.Status != InspectionStatus.Draft)
            return new(SaveInspectionAnswerOutcome.InspectionNotDraft);

        var question = inspection.Questions.SingleOrDefault(question => question.Id == questionId);
        if (question is null)
            return new(SaveInspectionAnswerOutcome.QuestionNotFound);

        // First guard: the editing screen must be based on the current revision.
        if (inspection.Version != expectedVersion)
            return new(SaveInspectionAnswerOutcome.ConcurrencyConflict);

        if (!Enum.IsDefined(typeof(InspectionAnswer), answer))
            return new(SaveInspectionAnswerOutcome.InvalidAnswer, Error: "Select a valid answer.");
        if (question.IsRequired && answer == InspectionAnswer.NotApplicable)
            return new(SaveInspectionAnswerOutcome.InvalidAnswer,
                Error: "A required question must receive Pass or Fail.");
        if (comment?.Trim().Length > InspectionQuestion.MaxCommentLength)
            return new(SaveInspectionAnswerOutcome.InvalidAnswer,
                Error: $"Comments cannot exceed {InspectionQuestion.MaxCommentLength} characters.");

        // Domain enforces the invariant as well, independent of this entry point.
        inspection.AnswerQuestion(questionId, answer, comment);

        // Second guard: EF checks for changes made after our read, before our write.
        if (!await _repository.TrySaveChangesAsync(cancellationToken))
            return new(SaveInspectionAnswerOutcome.ConcurrencyConflict);

        return new(SaveInspectionAnswerOutcome.Saved, inspection.Version);
    }
}
