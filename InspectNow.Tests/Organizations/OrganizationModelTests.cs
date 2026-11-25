using InspectNow.Domain.Organizations;
using InspectNow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InspectNow.Tests.Organizations;

public sealed class OrganizationModelTests
{
    [Fact]
    public void Membership_key_scopes_an_identity_to_one_organization()
    {
        using var db = CreateContext();
        var membership = db.Model.FindEntityType(typeof(OrganizationMembership))!;

        Assert.Equal(
            new[] { nameof(OrganizationMembership.OrganizationId), nameof(OrganizationMembership.Issuer), nameof(OrganizationMembership.Subject) },
            membership.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(typeof(Organization), Assert.Single(membership.GetForeignKeys()).PrincipalEntityType.ClrType);
    }

    [Fact]
    public void Migration_matches_the_model_and_creates_both_tables()
    {
        using var db = CreateContext();
        Assert.False(db.Database.HasPendingModelChanges());

        var sql = db.GetService<IMigrator>().GenerateScript("20261008123000_AddInspectionSubmission");
        Assert.Contains("CREATE TABLE \"Organizations\"", sql);
        Assert.Contains("CREATE TABLE \"OrganizationMemberships\"", sql);
    }

    private static InspectNowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InspectNowDbContext>()
            .UseNpgsql("Host=localhost;Database=inspectnow_tests")
            .Options;
        return new InspectNowDbContext(options);
    }
}
