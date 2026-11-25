namespace InspectNow.Infrastructure.Persistence;

public sealed class OrganizationScope
{
    public long? Id { get; private set; }

    public void Select(long id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        if (Id.HasValue)
            throw new InvalidOperationException("An organization has already been selected for this request.");
        Id = id;
    }
}
