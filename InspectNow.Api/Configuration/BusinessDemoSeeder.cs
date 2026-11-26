using System.Text.Json;
using InspectNow.Api.Authorization;
using InspectNow.Api.Controllers;
using InspectNow.Domain.Forms;
using InspectNow.Domain.Inspections;
using InspectNow.Domain.Organizations;
using InspectNow.Domain.Templates;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InspectNow.Api.Configuration;

internal static class BusinessDemoSeeder
{
    public static async Task Seed(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connection = new NpgsqlConnectionStringBuilder(DatabaseConnectionString.Resolve(configuration));
        if (!environment.IsDevelopment() || connection.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            connection.Database is not ("inspectnow" or "inspectnow_tests"))
            throw new InvalidOperationException("Demo seeding requires a local development database.");
        var password = configuration["Demo:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Supply a development-only Demo:Password to seed sample accounts.");

        await using var serviceScope = services.CreateAsyncScope();
        var db = serviceScope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
        var users = serviceScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var clock = serviceScope.ServiceProvider.GetRequiredService<TimeProvider>();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var now = clock.GetUtcNow();

        foreach (var sample in Samples)
        {
            var organization = await db.Organizations.SingleOrDefaultAsync(item => item.Name == sample.Name);
            if (organization is null)
            {
                organization = new Organization(sample.Name, sample.Type);
                db.Organizations.Add(organization);
                await db.SaveChangesAsync();
            }
            if (organization.Type != sample.Type)
                throw new InvalidOperationException("A workspace uses a reserved demo name. No demo changes were saved.");

            async Task<IdentityUser> EnsureMember(string prefix, WorkspaceRole role)
            {
                var email = $"{prefix}@{sample.Slug}.inspectnow.example";
                var user = await users.FindByEmailAsync(email);
                if (user is null)
                {
                    user = new IdentityUser { Email = email, UserName = email };
                    var result = await users.CreateAsync(user, password);
                    if (!result.Succeeded)
                        throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
                }
                else if (!await users.CheckPasswordAsync(user, password))
                    throw new InvalidOperationException("An existing demo account has a different password. No demo changes were saved.");

                if (!await db.OrganizationMemberships.AnyAsync(member => member.OrganizationId == organization.Id &&
                    member.Issuer == OrganizationAccess.LocalIssuer && member.Subject == user.Id))
                    db.OrganizationMemberships.Add(new OrganizationMembership(organization.Id, OrganizationAccess.LocalIssuer, user.Id, role));
                return user;
            }

            await EnsureMember("admin", WorkspaceRole.Admin);
            var respondent = await EnsureMember("member", WorkspaceRole.Inspector);
            if (sample.Type == OrganizationType.FieldOperations && !await db.Sites.IgnoreQueryFilters()
                .AnyAsync(site => site.OrganizationId == organization.Id && site.Name == "Demo · North warehouse"))
            {
                var site = new Site(organization.Id, "Demo · North warehouse");
                var template = new InspectionTemplate("Demo · Warehouse opening check");
                template.AddQuestion("Are all emergency exits clear?", true);
                template.AddQuestion("Is the loading bay clear of debris?", true);
                template.Publish();
                db.Sites.Add(site);
                db.Entry(template).Property(item => item.OrganizationId).CurrentValue = organization.Id;
                db.InspectionTemplates.Add(template);
                await db.SaveChangesAsync();
                var inspection = Inspection.Start(template, site.Name, now);
                inspection.Assign(site.Id, respondent.Id, DateOnly.FromDateTime(now.UtcDateTime).AddDays(7));
                db.Entry(inspection).Property(item => item.OrganizationId).CurrentValue = organization.Id;
                db.Inspections.Add(inspection);
            }
            if (!await db.Forms.IgnoreQueryFilters().AnyAsync(form => form.OrganizationId == organization.Id && form.Name == sample.Form))
            {
                var fields = new FormsController.FormField[]
                {
                    new(Guid.NewGuid(), "section", "Details", false, []),
                    new(Guid.NewGuid(), "text", sample.Prompt, true, []),
                    new(Guid.NewGuid(), "single", "Priority", true, ["Routine", "Urgent"]),
                    new(Guid.NewGuid(), "section", "Notes", false, []),
                    new(Guid.NewGuid(), "paragraph", "Additional details", false, [])
                };
                var form = new FormDefinition(organization.Id, sample.Form, sample.Kind,
                    JsonSerializer.Serialize(fields, JsonSerializerOptions.Web));
                form.Publish(now);
                db.Forms.Add(form);
                await db.SaveChangesAsync();

                var response = new FormResponse(organization.Id, form.Id, OrganizationAccess.LocalIssuer, respondent.Id);
                response.Save(JsonSerializer.Serialize(new Dictionary<Guid, string>
                {
                    [fields[1].Id] = sample.Answer, [fields[2].Id] = "Routine", [fields[4].Id] = "Illustrative demo record."
                }), now, submit: true);
                db.FormResponses.Add(response);
            }
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
    }

    private static readonly Sample[] Samples =
    [
        new("Demo · Atlas Facilities", "atlas", OrganizationType.FieldOperations, "Site handover checklist", "Inspection", "Site or zone", "North loading bay"),
        new("Demo · Cedar Training", "cedar", OrganizationType.Education, "Training completion check", "Assessment", "Course name", "Workplace induction"),
        new("Demo · Harbor Client Services", "harbor", OrganizationType.ClientServices, "Client onboarding intake", "Intake", "Service requested", "Onboarding consultation")
    ];

    private sealed record Sample(string Name, string Slug, OrganizationType Type, string Form, string Kind, string Prompt, string Answer);
}
