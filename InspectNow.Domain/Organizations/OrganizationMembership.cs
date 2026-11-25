namespace InspectNow.Domain.Organizations;

public sealed class OrganizationMembership
{
    public const int MaxIdentityPartLength = 255;

    public long OrganizationId { get; private set; }
    public string Issuer { get; private set; }
    public string Subject { get; private set; }

    public OrganizationMembership(long organizationId, string issuer, string subject)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(organizationId);
        ValidateIdentityPart(issuer, nameof(issuer));
        ValidateIdentityPart(subject, nameof(subject));
        OrganizationId = organizationId;
        Issuer = issuer;
        Subject = subject;
    }

    private static void ValidateIdentityPart(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > MaxIdentityPartLength)
            throw new ArgumentException($"Identity values cannot exceed {MaxIdentityPartLength} characters.", parameterName);
    }
}
