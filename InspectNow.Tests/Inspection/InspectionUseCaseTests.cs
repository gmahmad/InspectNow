using InspectNow.Application.Inspections;
using InspectNow.Domain.Inspections;
using InspectNow.Tests.Support;

namespace InspectNow.Tests.Inspections;

public sealed class InspectionUseCaseTests
{
    [Fact]
    public async Task Save_StaleClientVersion_DoesNotMutateOrSave()
    {
        var inspection = InspectionFixture.Create();
        var repository = new StubRepository(inspection);
        var version = inspection.Version;
        var result = await new SaveInspectionAnswer(repository).ExecuteAsync(
            inspection.Id, 401, InspectionAnswer.Pass, null, Guid.NewGuid());
        Assert.Equal(SaveInspectionAnswerOutcome.ConcurrencyConflict, result.Outcome);
        Assert.Null(inspection.Questions[0].Answer);
        Assert.Equal(version, inspection.Version);
        Assert.Equal(0, repository.SaveAttempts);
    }

    [Fact]
    public async Task Save_ConcurrentDatabaseChange_ReturnsConflictWithoutRetry()
    {
        var inspection = InspectionFixture.Create();
        var repository = new StubRepository(inspection) { SaveSucceeds = false };
        var result = await new SaveInspectionAnswer(repository).ExecuteAsync(
            inspection.Id, 401, InspectionAnswer.Pass, null, inspection.Version);
        Assert.Equal(SaveInspectionAnswerOutcome.ConcurrencyConflict, result.Outcome);
        Assert.Null(result.Version);
        Assert.Equal(1, repository.SaveAttempts);
    }

    [Fact]
    public async Task Save_ValidAnswer_ReturnsNewVersionAndNormalizedComment()
    {
        var inspection = InspectionFixture.Create();
        var originalVersion = inspection.Version;
        var repository = new StubRepository(inspection);
        var result = await new SaveInspectionAnswer(repository).ExecuteAsync(
            inspection.Id, 401, InspectionAnswer.Fail, "  Needs cleaning  ", originalVersion);
        Assert.Equal(SaveInspectionAnswerOutcome.Saved, result.Outcome);
        Assert.Equal(inspection.Version, result.Version);
        Assert.NotEqual(originalVersion, result.Version);
        Assert.Equal("Needs cleaning", inspection.Questions[0].Comment);
        Assert.Equal(1, repository.SaveAttempts);
    }

    [Fact]
    public async Task Save_UnknownQuestion_DoesNotSave()
    {
        var inspection = InspectionFixture.Create();
        var repository = new StubRepository(inspection);
        var result = await new SaveInspectionAnswer(repository).ExecuteAsync(
            inspection.Id, 999, InspectionAnswer.Pass, null, inspection.Version);
        Assert.Equal(SaveInspectionAnswerOutcome.QuestionNotFound, result.Outcome);
        Assert.Equal(0, repository.SaveAttempts);
    }

    [Fact]
    public async Task Submit_UsesInjectedClockAndReturnsPersistedRevision()
    {
        var inspection = InspectionFixture.Create();
        inspection.AnswerQuestion(401, InspectionAnswer.Pass, null);
        var repository = new StubRepository(inspection);
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        var result = await new SubmitInspection(repository, new FixedClock(now))
            .ExecuteAsync(inspection.Id, inspection.Version);
        Assert.Equal(SubmitInspectionOutcome.Submitted, result.Outcome);
        Assert.Equal(now, result.SubmittedAtUtc);
        Assert.Equal(inspection.Version, result.Version);
        Assert.Equal(1, repository.SaveAttempts);
    }

    [Fact]
    public async Task Submit_StaleClientVersion_DoesNotSubmitOrSave()
    {
        var inspection = InspectionFixture.Create();
        inspection.AnswerQuestion(401, InspectionAnswer.Pass, null);
        var repository = new StubRepository(inspection);
        var result = await new SubmitInspection(repository, TimeProvider.System)
            .ExecuteAsync(inspection.Id, Guid.NewGuid());
        Assert.Equal(SubmitInspectionOutcome.ConcurrencyConflict, result.Outcome);
        Assert.Equal(InspectionStatus.Draft, inspection.Status);
        Assert.Equal(0, repository.SaveAttempts);
    }

    [Fact]
    public async Task Submit_ConcurrentDatabaseChange_ReturnsConflictWithoutSuccessMetadata()
    {
        var inspection = InspectionFixture.Create();
        inspection.AnswerQuestion(401, InspectionAnswer.Pass, null);
        var repository = new StubRepository(inspection) { SaveSucceeds = false };
        var result = await new SubmitInspection(repository, TimeProvider.System)
            .ExecuteAsync(inspection.Id, inspection.Version);
        Assert.Equal(SubmitInspectionOutcome.ConcurrencyConflict, result.Outcome);
        Assert.Null(result.Version);
        Assert.Null(result.SubmittedAtUtc);
        Assert.Equal(1, repository.SaveAttempts);
    }

    [Fact]
    public async Task Submit_MissingAnswers_DoesNotSave()
    {
        var inspection = InspectionFixture.Create();
        var repository = new StubRepository(inspection);
        var result = await new SubmitInspection(repository, TimeProvider.System)
            .ExecuteAsync(inspection.Id, inspection.Version);
        Assert.Equal(SubmitInspectionOutcome.RequiredAnswersMissing, result.Outcome);
        Assert.Equal(InspectionStatus.Draft, inspection.Status);
        Assert.Equal(0, repository.SaveAttempts);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubRepository(Inspection inspection) : IInspectionRepository
    {
        public bool SaveSucceeds { get; init; } = true;
        public int SaveAttempts { get; private set; }
        public void Add(Inspection value) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<InspectionDetailsResult?> GetDetailsAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Inspection?> GetForUpdateAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult<Inspection?>(id == inspection.Id ? inspection : null);
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            return Task.FromResult(SaveSucceeds);
        }
    }
}
