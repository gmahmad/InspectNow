using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InspectNow.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class TeamWorkflowTests
{
    [Fact]
    public async Task Admin_assigns_and_inspector_submits_only_their_own_work()
    {
        using var factory = new InspectNowApiFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<InspectNowDbContext>().Database.MigrateAsync();

        using var admin = Client(factory);
        using var inspector = Client(factory);
        using var colleague = Client(factory);
        var adminEmail = $"admin-{Guid.NewGuid():N}@example.test";
        const string password = "Team-workflow-124!";
        using (var missingCsrf = await admin.PostAsJsonAsync("/api/account/register", new { email = adminEmail, password }))
            Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        await Post(admin, "/api/account/register", new { email = adminEmail, password }, HttpStatusCode.NoContent);
        await Post(admin, "/api/organizations", new { name = "Unapproved workspace" }, HttpStatusCode.Forbidden);
        await using (var ownerScope = factory.Services.CreateAsyncScope())
        {
            var db = ownerScope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
            var owner = await db.Users.SingleAsync(user => user.Email == adminEmail);
            factory.Services.GetRequiredService<IConfiguration>()["Platform:OwnerUserId"] = owner.Id;
        }
        var workspace = await Post(admin, "/api/organizations", new { name = "Campus Operations" }, HttpStatusCode.Created);
        var organizationId = workspace.GetProperty("id").GetInt64();
        SelectOrganization(admin, organizationId);
        Assert.Equal("Admin", Assert.Single((await admin.GetFromJsonAsync<JsonElement[]>("/api/organizations"))!)
            .GetProperty("role").GetString());
        await Post(admin, "/api/team/invitations", new { role = "Admin" }, HttpStatusCode.Created);

        async Task<string> InviteInspector(HttpClient client)
        {
            var email = $"inspector-{Guid.NewGuid():N}@example.test";
            await Post(client, "/api/account/register", new { email, password }, HttpStatusCode.NoContent);
            var invite = await Post(admin, "/api/team/invitations", new { role = "Inspector" }, HttpStatusCode.Created);
            var code = invite.GetProperty("code").GetString()!;
            await Post(client, "/api/team/join", new { code }, HttpStatusCode.OK);
            SelectOrganization(client, organizationId);
            return code;
        }

        var redeemedCode = await InviteInspector(inspector);
        Assert.Equal("Inspector", Assert.Single((await inspector.GetFromJsonAsync<JsonElement[]>("/api/organizations"))!)
            .GetProperty("role").GetString());
        await InviteInspector(colleague);
        await Post(colleague, "/api/team/join", new { code = redeemedCode }, HttpStatusCode.BadRequest);
        await Post(inspector, "/api/team/invitations", new { role = "Manager" }, HttpStatusCode.Forbidden);
        await Post(inspector, "/api/inspection-templates", new { name = "Forbidden checklist" }, HttpStatusCode.Forbidden);

        var site = await Post(admin, "/api/sites", new { name = "South Campus" }, HttpStatusCode.Created);
        var template = await Post(admin, "/api/inspection-templates", new { name = "Daily checks" }, HttpStatusCode.Created);
        var templateId = template.GetProperty("id").GetInt64();
        await Post(admin, $"/api/inspection-templates/{templateId}/questions", new { text = "Exits clear?", isRequired = true }, HttpStatusCode.Created);
        await Post(admin, $"/api/inspection-templates/{templateId}/publish", null, HttpStatusCode.NoContent);

        var members = await admin.GetFromJsonAsync<JsonElement[]>("/api/team");
        var accountEmail = (await inspector.GetFromJsonAsync<JsonElement>("/api/account")).GetProperty("email").GetString();
        var assignee = Assert.Single(members!, member => member.GetProperty("email").GetString() == accountEmail);
        var assignment = await Post(admin, "/api/assignments", new
        {
            templateId,
            siteId = site.GetProperty("id").GetInt64(),
            assigneeId = assignee.GetProperty("id").GetString(),
            dueDate = "2027-01-10"
        }, HttpStatusCode.Created);
        var accessUrl = $"/api/team/{assignee.GetProperty("id").GetString()}/access";
        var blockedAccess = new { role = "Inspector", canUseForms = false, canUseInspections = false };
        await Send(admin, HttpMethod.Put, accessUrl, blockedAccess, HttpStatusCode.Conflict);
        var id = assignment.GetProperty("id").GetInt64();
        Assert.Single((await inspector.GetFromJsonAsync<JsonElement[]>("/api/assignments"))!);

        const string forms = "/api/forms";
        var fieldId = Guid.NewGuid();
        var numberId = Guid.NewGuid();
        var definition = new
        {
            name = "Equipment intake", kind = "Intake",
            fields = new[]
            {
                new { id = fieldId, type = "boolean", label = "Is the device powered?", required = true },
                new { id = numberId, type = "number", label = "Quantity", required = true }
            }
        };
        var form = await Post(admin, forms, definition, HttpStatusCode.Created);
        var formUrl = $"{forms}/{form.GetProperty("id").GetInt64()}";
        Assert.Empty((await inspector.GetFromJsonAsync<JsonElement[]>(forms))!);
        await Post(admin, $"{formUrl}/publish", new { expectedVersion = form.GetProperty("version").GetGuid() }, HttpStatusCode.OK);
        await Send(admin, HttpMethod.Put, formUrl, definition, HttpStatusCode.Conflict);
        Assert.Single((await inspector.GetFromJsonAsync<JsonElement[]>(forms))!);
        var partial = await Send(inspector, HttpMethod.Put, $"{formUrl}/draft", new
        {
            answers = new Dictionary<Guid, object> { [fieldId] = false }, submit = false
        }, HttpStatusCode.OK);
        var colleagueDraft = await colleague.GetFromJsonAsync<JsonElement>($"{formUrl}/draft");
        Assert.Equal(JsonValueKind.Null, colleagueDraft.GetProperty("version").ValueKind);
        var partialVersion = partial.GetProperty("version").GetGuid();
        await Send(inspector, HttpMethod.Put, $"{formUrl}/draft", new
        {
            answers = new Dictionary<Guid, object> { [fieldId] = false }, expectedVersion = partialVersion, submit = true
        }, HttpStatusCode.BadRequest);
        var completed = new
        {
            answers = new Dictionary<Guid, object> { [fieldId] = false, [numberId] = 4 },
            expectedVersion = partialVersion, submit = true
        };
        var submittedResponse = await Send(inspector, HttpMethod.Put, $"{formUrl}/draft", completed, HttpStatusCode.OK);
        await Send(inspector, HttpMethod.Put, $"{formUrl}/draft", completed, HttpStatusCode.Conflict);
        using var responseAccess = await inspector.GetAsync($"{forms}/responses");
        Assert.Equal(HttpStatusCode.Forbidden, responseAccess.StatusCode);
        var library = await admin.GetFromJsonAsync<JsonElement>($"{forms}/responses");
        Assert.Equal(1, library.GetProperty("total").GetInt32());
        var responseUrl = $"{forms}/responses/{submittedResponse.GetProperty("id").GetInt64()}";
        var storedResponse = await admin.GetFromJsonAsync<JsonElement>(responseUrl);
        Assert.False(storedResponse.GetProperty("response").GetProperty("answers").GetProperty(fieldId.ToString()).GetBoolean());

        const string rolesUrl = "/api/team/roles";
        await Post(inspector, rolesUrl, new { name = "Quality" }, HttpStatusCode.Forbidden);
        var quality = (await Post(admin, rolesUrl, new { name = "Quality" }, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var dispatch = (await Post(admin, rolesUrl, new { name = "Dispatch" }, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        await Post(admin, rolesUrl, new { name = "quality" }, HttpStatusCode.Conflict);
        var assignmentsUrl = $"/api/team/{assignee.GetProperty("id").GetString()}/roles";
        await Send(admin, HttpMethod.Put, assignmentsUrl, new { roleIds = new[] { dispatch } }, HttpStatusCode.NoContent);

        var currentForm = await admin.GetFromJsonAsync<JsonElement>(formUrl);
        var restricted = await Send(admin, HttpMethod.Put, $"{formUrl}/audience", new
        {
            expectedVersion = currentForm.GetProperty("version").GetGuid(),
            audience = new { mode = "roles", roles = new[] { quality.ToString(), dispatch.ToString() } }
        }, HttpStatusCode.OK);
        Assert.Single((await inspector.GetFromJsonAsync<JsonElement[]>(forms))!);
        Assert.Empty((await colleague.GetFromJsonAsync<JsonElement[]>(forms))!);
        using var restrictedForm = await colleague.GetAsync(formUrl);
        using var restrictedDraft = await colleague.GetAsync($"{formUrl}/draft");
        Assert.Equal(HttpStatusCode.NotFound, restrictedForm.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, restrictedDraft.StatusCode);
        await Send(colleague, HttpMethod.Put, $"{formUrl}/draft", completed, HttpStatusCode.NotFound);

        var shared = await Send(admin, HttpMethod.Put, $"{formUrl}/audience", new
        {
            expectedVersion = restricted.GetProperty("version").GetGuid(),
            audience = new { mode = "public", roles = Array.Empty<string>() }
        }, HttpStatusCode.OK);
        using var guest = Client(factory);
        var publicUrl = $"{forms}/public/{shared.GetProperty("publicKey").GetGuid()}";
        var sharedForm = await guest.GetFromJsonAsync<JsonElement>(publicUrl);
        Assert.Equal("Equipment intake", sharedForm.GetProperty("name").GetString());
        Assert.False(sharedForm.TryGetProperty("version", out _));
        await Post(guest, $"{publicUrl}/responses", new { answers = new Dictionary<Guid, object>() }, HttpStatusCode.BadRequest);
        await Post(guest, $"{publicUrl}/responses", new { completed.answers }, HttpStatusCode.NoContent);
        var updatedLibrary = await admin.GetFromJsonAsync<JsonElement>($"{forms}/responses");
        Assert.Equal(2, updatedLibrary.GetProperty("total").GetInt32());

        await Send(admin, HttpMethod.Put, $"{formUrl}/audience", new
        {
            expectedVersion = shared.GetProperty("version").GetGuid(),
            audience = new { mode = "workspace", roles = Array.Empty<string>() }
        }, HttpStatusCode.OK);
        using var revoked = await guest.GetAsync(publicUrl);
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        await Post(guest, $"{publicUrl}/responses", new { completed.answers }, HttpStatusCode.NotFound);

        var secondWorkspace = await Post(admin, "/api/organizations", new { name = "Other workspace", type = "Education" }, HttpStatusCode.Created);
        Assert.Equal("Education", secondWorkspace.GetProperty("type").GetString());
        Assert.False(secondWorkspace.GetProperty("canUseInspections").GetBoolean());
        admin.DefaultRequestHeaders.Remove("X-Organization-Id");
        SelectOrganization(admin, secondWorkspace.GetProperty("id").GetInt64());
        Assert.Empty((await admin.GetFromJsonAsync<JsonElement[]>(rolesUrl))!);
        await Send(admin, HttpMethod.Put, assignmentsUrl, new { roleIds = new[] { quality } }, HttpStatusCode.BadRequest);
        using var crossWorkspace = await admin.GetAsync(responseUrl);
        Assert.Equal(HttpStatusCode.NotFound, crossWorkspace.StatusCode);
        using var disabledModule = await admin.GetAsync("/api/inspections");
        Assert.Equal(HttpStatusCode.Forbidden, disabledModule.StatusCode);
        admin.DefaultRequestHeaders.Remove("X-Organization-Id");
        SelectOrganization(admin, organizationId);
        Assert.Empty((await colleague.GetFromJsonAsync<JsonElement[]>("/api/assignments"))!);
        using var hidden = await colleague.GetAsync($"/api/inspections/{id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var draft = await inspector.GetFromJsonAsync<JsonElement>($"/api/inspections/{id}");
        var questionId = draft.GetProperty("questions")[0].GetProperty("id").GetInt64();
        var answer = await Send(inspector, HttpMethod.Put, $"/api/inspections/{id}/questions/{questionId}/answer",
            new { answer = "Fail", comment = "Boxes in front of exit.", expectedVersion = draft.GetProperty("version").GetGuid() }, HttpStatusCode.OK);
        await Post(inspector, $"/api/inspections/{id}/submit", new { expectedVersion = answer.GetProperty("version").GetGuid() }, HttpStatusCode.OK);
        var result = Assert.Single((await admin.GetFromJsonAsync<JsonElement[]>("/api/assignments"))!);
        Assert.Equal("Submitted", result.GetProperty("status").GetString());

        const string actions = "/api/corrective-actions";
        var actionInput = new { inspectionId = id, questionId, assigneeId = assignee.GetProperty("id").GetString(),
            instructions = "Clear the exit and confirm access.", dueDate = "2027-01-12" };
        await Post(inspector, actions, actionInput, HttpStatusCode.Forbidden);
        var createdAction = await Post(admin, actions, actionInput, HttpStatusCode.Created);
        await Post(admin, actions, actionInput, HttpStatusCode.Conflict);
        var actionUrl = $"{actions}/{createdAction.GetProperty("id").GetInt64()}";
        var otherList = await colleague.GetFromJsonAsync<JsonElement>(actions);
        Assert.Empty(otherList.GetProperty("items").EnumerateArray());

        async Task<JsonElement> ReadAction(HttpClient client)
        {
            var list = await client.GetFromJsonAsync<JsonElement>(actions);
            return Assert.Single(list.GetProperty("items").EnumerateArray());
        }

        var open = await ReadAction(inspector);
        Assert.Equal("Open", open.GetProperty("status").GetString());
        Assert.Equal("Boxes in front of exit.", open.GetProperty("findingNote").GetString());
        var initialVersion = open.GetProperty("version").GetGuid();
        var resolutionInput = new { expectedVersion = initialVersion, resolution = "Exit cleared." };
        await Post(colleague, $"{actionUrl}/resolve", resolutionInput, HttpStatusCode.NotFound);
        await Post(inspector, $"{actionUrl}/resolve", resolutionInput, HttpStatusCode.NoContent);
        await Post(inspector, $"{actionUrl}/resolve", resolutionInput, HttpStatusCode.Conflict);

        var review = await ReadAction(admin);
        Assert.Equal("ReadyForReview", review.GetProperty("status").GetString());
        var reviewVersion = review.GetProperty("version").GetGuid();
        await Post(inspector, $"{actionUrl}/review", new { expectedVersion = reviewVersion, approve = true }, HttpStatusCode.Forbidden);
        await Post(admin, $"{actionUrl}/review", new { expectedVersion = reviewVersion, approve = false, note = "Add how access was checked." }, HttpStatusCode.NoContent);
        var reopened = await ReadAction(inspector);
        Assert.Equal("Open", reopened.GetProperty("status").GetString());
        await Post(inspector, $"{actionUrl}/resolve",
            new { expectedVersion = reopened.GetProperty("version").GetGuid(), resolution = "Cleared the exit and tested that the door opens freely." },
            HttpStatusCode.NoContent);
        var updatedReview = await ReadAction(admin);
        await Post(admin, $"{actionUrl}/review", new { expectedVersion = updatedReview.GetProperty("version").GetGuid(), approve = true },
            HttpStatusCode.NoContent);
        var closed = await ReadAction(inspector);
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.True(closed.GetProperty("closedAtUtc").GetDateTimeOffset() > DateTimeOffset.MinValue);

        await Post(inspector, "/api/account/logout", null, HttpStatusCode.NoContent);
        using var signedOut = await inspector.GetAsync($"/api/inspections/{id}");
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
        await Post(inspector, "/api/account/login", new { email = accountEmail, password }, HttpStatusCode.NoContent);
        Assert.Single((await inspector.GetFromJsonAsync<JsonElement[]>("/api/assignments"))!);
        await Send(inspector, HttpMethod.Put, accessUrl, blockedAccess, HttpStatusCode.Forbidden);
        await Send(admin, HttpMethod.Put, accessUrl, blockedAccess, HttpStatusCode.NoContent);
        using var blockedForms = await inspector.GetAsync("/api/forms");
        using var blockedInspections = await inspector.GetAsync("/api/inspections");
        Assert.Equal(HttpStatusCode.Forbidden, blockedForms.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, blockedInspections.StatusCode);
        await Send(admin, HttpMethod.Put, accessUrl,
            new { role = "Manager", canUseForms = true, canUseInspections = false }, HttpStatusCode.NoContent);
        var changedAccess = Assert.Single((await inspector.GetFromJsonAsync<JsonElement[]>("/api/organizations"))!);
        Assert.Equal("Manager", changedAccess.GetProperty("role").GetString());
        Assert.True(changedAccess.GetProperty("canUseForms").GetBoolean());
        Assert.False(changedAccess.GetProperty("canUseInspections").GetBoolean());
    }

    private static HttpClient Client(InspectNowApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });

    private static void SelectOrganization(HttpClient client, long id) =>
        client.DefaultRequestHeaders.Add("X-Organization-Id", id.ToString(CultureInfo.InvariantCulture));

    private static Task<JsonElement> Post(HttpClient client, string path, object? body, HttpStatusCode status) =>
        Send(client, HttpMethod.Post, path, body, status);

    private static async Task<JsonElement> Send(HttpClient client, HttpMethod method, string path, object? body, HttpStatusCode status)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/account/csrf");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-Token", csrf.GetProperty("token").GetString());
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        Assert.Equal(status, response.StatusCode);
        return response.StatusCode == HttpStatusCode.NoContent ? default : await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
