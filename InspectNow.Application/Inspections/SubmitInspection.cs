using InspectNow.Domain.Inspections;

namespace InspectNow.Application.Inspections;

public sealed class SubmitInspection
{
    private readonly IInspectionRepository _repository;
    private readonly TimeProvider _timeProvider;

    public SubmitInspection(IInspectionRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<SubmitInspectionResult> ExecuteAsync(
        long inspectionId, Guid expectedVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedVersion == Guid.Empty)
            return new(SubmitInspectionOutcome.InvalidVersion);

        var inspection = await _repository.GetForUpdateAsync(inspectionId, cancellationToken);
        if (inspection is null)
            return new(SubmitInspectionOutcome.InspectionNotFound);
        if (inspection.Status != InspectionStatus.Draft)
            return new(SubmitInspectionOutcome.InspectionNotDraft);
        if (inspection.Version != expectedVersion)
            return new(SubmitInspectionOutcome.ConcurrencyConflict);
        if (inspection.Questions.Any(question => question.IsRequired &&
            question.Answer is not (InspectionAnswer.Pass or InspectionAnswer.Fail)))
            return new(SubmitInspectionOutcome.RequiredAnswersMissing);

        inspection.Submit(_timeProvider.GetUtcNow());
        if (!await _repository.TrySaveChangesAsync(cancellationToken))
            return new(SubmitInspectionOutcome.ConcurrencyConflict);

        return new(SubmitInspectionOutcome.Submitted,
            inspection.Version, inspection.SubmittedAtUtc);
    }
}
