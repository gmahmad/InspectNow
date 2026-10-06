namespace InspectNow.Tests.Support;

// Unit tests simulate objects loaded from persistence. Real ID generation is
// verified by PostgreSQL integration tests; production setters remain private.
internal static class PersistedEntityFixture
{
    internal static void AssignId<T>(T entity, long id) where T : class
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id));

        var property = typeof(T).GetProperty("Id")
            ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id property.");
        property.SetValue(entity, id);
    }
}
