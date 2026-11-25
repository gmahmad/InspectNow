using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using InspectNow.Api.Authorization;
using InspectNow.Api.Controllers;
using InspectNow.Application.Inspections;
using InspectNow.Application.Templates;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InspectNow.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class OrganizationAccessTests
{
    [Fact]
    public async Task Organization_members_can_only_read_and_change_their_own_records()
    {
        using var factory = new InspectNowApiFactory();
        using var owner = await factory.CreateMemberClientAsync();
        using var other = await factory.CreateMemberClientAsync();
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });

        const string templates = "/api/inspection-templates";
        const string inspections = "/api/inspections";
        using var unauthenticated = await anonymous.GetAsync(templates);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var ownerOrganizations = await owner.GetFromJsonAsync<OrganizationsController.OrganizationSummary[]>("/api/organizations");
        var ownerId = Assert.Single(ownerOrganizations!).Id;
        var otherHeader = other.DefaultRequestHeaders.GetValues(OrganizationAccess.Header).Single();
        Assert.NotEqual(ownerId.ToString(CultureInfo.InvariantCulture), otherHeader);

        using var create = await owner.PostAsJsonAsync(templates, new { name = "Private checklist" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var template = (await create.Content.ReadFromJsonAsync<CreateTemplateResult>())!;
        var templateUrl = $"{templates}/{template.Id}";
        using var question = await owner.PostAsJsonAsync($"{templateUrl}/questions", new { text = "Clean?", isRequired = true });
        Assert.Equal(HttpStatusCode.Created, question.StatusCode);
        using var publish = await owner.PostAsync($"{templateUrl}/publish", null);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        using var start = await owner.PostAsJsonAsync(inspections, new { templateId = template.Id, siteName = "Private site" });
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var inspectionUrl = start.Headers.Location!;
        var inspection = (await owner.GetFromJsonAsync<InspectionDetailsResult>(inspectionUrl))!;

        using var hiddenTemplate = await other.GetAsync(templateUrl);
        using var hiddenInspection = await other.GetAsync(inspectionUrl);
        using var foreignStart = await other.PostAsJsonAsync(inspections, new { templateId = template.Id, siteName = "Other site" });
        using var foreignSubmit = await other.PostAsJsonAsync($"{inspectionUrl}/submit", new { expectedVersion = inspection.Version });
        Assert.Equal(HttpStatusCode.NotFound, hiddenTemplate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hiddenInspection.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignStart.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignSubmit.StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<TemplatePageResult>(templates))!.Items);
        Assert.Empty((await other.GetFromJsonAsync<InspectionPageResult>(inspections))!.Items);

        other.DefaultRequestHeaders.Remove(OrganizationAccess.Header);
        other.DefaultRequestHeaders.Add(OrganizationAccess.Header, ownerId.ToString(CultureInfo.InvariantCulture));
        using var unauthorizedOrganization = await other.GetAsync(templateUrl);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedOrganization.StatusCode);

        other.DefaultRequestHeaders.Remove(OrganizationAccess.Header);
        using var missingOrganization = await other.GetAsync(templates);
        Assert.Equal(HttpStatusCode.Forbidden, missingOrganization.StatusCode);
    }
}
