using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using InspectNow.Api.Authorization;
using InspectNow.Domain.Forms;
using InspectNow.Domain.Organizations;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InspectNow.Api.Controllers;

[ApiController]
[Authorize(Policy = OrganizationAccess.Policy)]
[RequireWorkspaceModule(WorkspaceModules.Forms)]
[Route("api/forms")]
public sealed class FormsController(InspectNowDbContext db, OrganizationScope scope, TimeProvider clock) : ControllerBase
{
    private static readonly string[] Kinds = ["General", "Survey", "Inspection", "Assessment", "Intake", "Request", "Audit"];
    private static readonly string[] Types = ["section", "text", "paragraph", "email", "number", "date", "time", "single", "multiple", "boolean", "rating"];
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private const string PublicFormUnavailable = "This form link is no longer available.";

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = await VisibleForms().AsNoTracking()
            .OrderBy(form => form.Name).ThenBy(form => form.Id)
            .Select(form => new { form.Id, form.Name, form.Kind, form.PublishedAtUtc }).ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:long:min(1)}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
    {
        var form = await VisibleForms().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return form is null ? NotFound() : Ok(Detail(form));
    }

    [HttpPost, ManagerOnly, RequestSizeLimit(256_000)]
    public async Task<IActionResult> Create(FormInput input, CancellationToken cancellationToken)
    {
        if (ValidateForm(input) is { } error) return Invalid(error);
        var form = new FormDefinition(scope.Id!.Value, input.Name.Trim(), input.Kind,
            JsonSerializer.Serialize(input.Fields, Json));
        if (input.Audience is { } audience && !await ApplyAudience(form, audience, cancellationToken))
            return Invalid("Choose a valid audience and roles from this workspace.");
        db.Forms.Add(form);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = form.Id }, Detail(form));
    }

    [HttpPut("{id:long:min(1)}"), ManagerOnly, RequestSizeLimit(256_000)]
    public async Task<IActionResult> Edit(long id, FormInput input, CancellationToken cancellationToken)
    {
        if (ValidateForm(input) is { } error) return Invalid(error);
        var form = await db.Forms.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (form is null) return NotFound();
        if (form.PublishedAtUtc is not null || form.Version != input.ExpectedVersion) return Stale();
        if (input.Audience is { } audience && !await ApplyAudience(form, audience, cancellationToken))
            return Invalid("Choose a valid audience and roles from this workspace.");
        form.SaveDraft(input.Name.Trim(), input.Kind, JsonSerializer.Serialize(input.Fields, Json));
        return await Save(cancellationToken) ? Ok(Detail(form)) : Stale();
    }

    [HttpPut("{id:long:min(1)}/audience"), ManagerOnly]
    public async Task<IActionResult> UpdateAudience(long id, AudienceUpdate input, CancellationToken cancellationToken)
    {
        var form = await db.Forms.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (form is null) return NotFound();
        if (form.Version != input.ExpectedVersion) return Stale();
        if (!await ApplyAudience(form, input.Audience, cancellationToken))
            return Invalid("Choose a valid audience and roles from this workspace.");
        return await Save(cancellationToken) ? Ok(Detail(form)) : Stale();
    }

    [HttpGet("public/{key:guid}"), AllowAnonymous]
    public async Task<IActionResult> PublicForm(Guid key, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var form = await PublicForms(key).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return form is null ? NotFound(new { detail = PublicFormUnavailable }) : Ok(new { form.Name, form.Kind, fields = Fields(form) });
    }

    [HttpPost("public/{key:guid}/responses"), AllowAnonymous, EnableRateLimiting("public-form"), RequestSizeLimit(512_000)]
    public async Task<IActionResult> SubmitPublic(Guid key, PublicAnswerInput input, CancellationToken cancellationToken)
    {
        var form = await PublicForms(key).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (form is null) return NotFound(new { detail = PublicFormUnavailable });
        if (ValidateAnswers(Fields(form), input.Answers, true) is { } error) return Invalid(error);
        var response = new FormResponse(form.OrganizationId, form.Id, "inspectnow-public", Guid.NewGuid().ToString());
        response.Save(JsonSerializer.Serialize(input.Answers, Json), clock.GetUtcNow(), true);
        db.FormResponses.Add(response);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long:min(1)}/publish"), ManagerOnly]
    public async Task<IActionResult> Publish(long id, VersionInput input, CancellationToken cancellationToken)
    {
        var form = await db.Forms.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (form is null) return NotFound();
        if (form.PublishedAtUtc is not null || form.Version != input.ExpectedVersion) return Stale();
        if (!Fields(form).Any(field => field.Type != "section")) return Invalid("Add a question before publishing.");
        form.Publish(clock.GetUtcNow());
        return await Save(cancellationToken) ? Ok(Detail(form)) : Stale();
    }

    [HttpGet("{id:long:min(1)}/draft")]
    public async Task<IActionResult> Draft(long id, CancellationToken cancellationToken)
    {
        if (!TryIdentity(out var issuer, out var subject)) return Forbid();
        if (!await FillableForms().AnyAsync(form => form.Id == id, cancellationToken)) return NotFound();
        var response = await OwnDraft(id, issuer, subject).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return response is null ? Ok(new { answers = new { }, version = (Guid?)null }) : Ok(ResponseDetail(response));
    }

    [HttpPut("{id:long:min(1)}/draft"), RequestSizeLimit(512_000)]
    public async Task<IActionResult> SaveDraft(long id, AnswerInput input, CancellationToken cancellationToken)
    {
        if (!TryIdentity(out var issuer, out var subject)) return Forbid();
        var form = await FillableForms().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (form is null) return NotFound();
        if (ValidateAnswers(Fields(form), input.Answers, input.Submit) is { } error) return Invalid(error);
        var draft = await OwnDraft(id, issuer, subject).SingleOrDefaultAsync(cancellationToken);
        if (draft?.Version != input.ExpectedVersion || (input.Submit && draft is null)) return Stale();
        if (draft is null)
        {
            draft = new FormResponse(form.OrganizationId, form.Id, issuer, subject);
            db.FormResponses.Add(draft);
        }
        draft.Save(JsonSerializer.Serialize(input.Answers, Json), clock.GetUtcNow(), input.Submit);
        return await Save(cancellationToken) ? Ok(ResponseDetail(draft)) : Stale();
    }

    [HttpGet("responses"), ManagerOnly]
    public async Task<IActionResult> Responses([FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery] long? formId = null, CancellationToken cancellationToken = default)
    {
        const int size = 25;
        var query = db.FormResponses.AsNoTracking().Where(response => response.SubmittedAtUtc != null &&
            (!formId.HasValue || response.FormId == formId));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(response => response.SubmittedAtUtc).ThenByDescending(response => response.Id)
            .Skip((page - 1) * size).Take(size).Join(db.Forms,
                response => response.FormId, form => form.Id,
                (response, form) => new { response.Id, response.FormId, response.SubmittedAtUtc, form.Name, form.Kind })
            .OrderByDescending(response => response.SubmittedAtUtc).ThenByDescending(response => response.Id).ToListAsync(cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { items, total, page, size });
    }

    [HttpGet("responses/{id:long:min(1)}"), ManagerOnly]
    public async Task<IActionResult> ReadResponse(long id, CancellationToken cancellationToken)
    {
        var response = await db.FormResponses.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == id && item.SubmittedAtUtc != null, cancellationToken);
        if (response is null) return NotFound();
        var form = await db.Forms.AsNoTracking().SingleAsync(item => item.Id == response.FormId, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { form = Detail(form), response = ResponseDetail(response) });
    }

    private IQueryable<FormDefinition> VisibleForms() =>
        scope.Role == WorkspaceRole.Inspector ? FillableForms() : db.Forms;

    private IQueryable<FormDefinition> FillableForms()
    {
        var roles = scope.CustomRoleIds.Select(id => id.ToString()).Append(scope.Role.ToString()).ToArray();
        return db.Forms.Where(form => form.PublishedAtUtc != null &&
            (form.AudienceRoles.Length == 0 || form.AudienceRoles.Any(role => roles.Contains(role))));
    }

    private IQueryable<FormDefinition> PublicForms(Guid key) =>
        db.Forms.IgnoreQueryFilters().Where(form => form.PublicKey == key && form.PublishedAtUtc != null);

    private async Task<bool> ApplyAudience(FormDefinition form, AudienceInput audience, CancellationToken cancellationToken)
    {
        if (audience.Mode is "workspace" or "public" && audience.Roles.Length == 0)
        {
            form.SetAudience([], audience.Mode == "public");
            return true;
        }
        if (audience.Mode != "roles" || audience.Roles.Length == 0) return false;
        var builtIn = Enum.GetNames<WorkspaceRole>();
        var roles = audience.Roles.Distinct(StringComparer.Ordinal).ToArray();
        var custom = new List<Guid>();
        foreach (var role in roles.Where(role => !builtIn.Contains(role)))
        {
            if (!Guid.TryParseExact(role, "D", out var id) || id.ToString() != role) return false;
            custom.Add(id);
        }
        if (await db.OrganizationRoles.CountAsync(role => custom.Contains(role.Id), cancellationToken) != custom.Count)
            return false;
        form.SetAudience(roles, false);
        return true;
    }

    private IQueryable<FormResponse> OwnDraft(long id, string issuer, string subject) => db.FormResponses.Where(
        item => item.FormId == id && item.Issuer == issuer && item.Subject == subject && item.SubmittedAtUtc == null);

    private bool TryIdentity(out string issuer, out string subject)
    {
        issuer = (OrganizationAccess.IsLocal(User) ? OrganizationAccess.LocalIssuer : User.FindFirst("iss")?.Value) ?? "";
        subject = OrganizationAccess.Subject(User) ?? "";
        return issuer.Length is > 0 and <= 255 && subject.Length is > 0 and <= 255;
    }

    private async Task<bool> Save(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return false; }
    }

    private IActionResult Stale() => Conflict(new { detail = "This record changed. Reopen it to load the latest version." });
    private IActionResult Invalid(string detail) => BadRequest(new { detail });
    private static FormField[] Fields(FormDefinition form) => JsonSerializer.Deserialize<FormField[]>(form.FieldsJson, Json)!;
    private object Detail(FormDefinition form) => new
    {
        form.Id, form.Name, form.Kind, form.PublishedAtUtc, form.Version, fields = Fields(form),
        CanRespond = form.AudienceRoles.Length == 0 || form.AudienceRoles.Contains(scope.Role.ToString()) ||
            scope.CustomRoleIds.Any(id => form.AudienceRoles.Contains(id.ToString())),
        Audience = new { Mode = form.PublicKey.HasValue ? "public" : form.AudienceRoles.Length == 0 ? "workspace" : "roles",
            Roles = form.AudienceRoles },
        PublicKey = scope.Role != WorkspaceRole.Inspector && form.PublishedAtUtc.HasValue ? form.PublicKey : null
    };
    private static object ResponseDetail(FormResponse response) => new
    {
        response.Id, response.Version, response.SubmittedAtUtc,
        answers = JsonSerializer.Deserialize<Dictionary<Guid, JsonElement>>(response.AnswersJson, Json)
    };

    private static string? ValidateForm(FormInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || !Kinds.Contains(input.Kind)) return "Give the form a name and choose a category.";
        if (input.Fields is null || input.Fields.Count > 150) return "Use up to 150 sections and questions per form.";
        var ids = new HashSet<Guid>();
        foreach (var field in input.Fields)
        {
            if (field is null || field.Id == Guid.Empty || !ids.Add(field.Id) ||
                string.IsNullOrWhiteSpace(field.Label) || field.Label.Length > 240 || !Types.Contains(field.Type))
                return "Each field needs a unique ID, a label, and a supported type.";
            if (field.Type is "single" or "multiple")
            {
                if (field.Options is null || field.Options.Count is < 2 or > 20 ||
                    field.Options.Any(option => string.IsNullOrWhiteSpace(option) || option.Length > 120) ||
                    field.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != field.Options.Count)
                    return $"Enter 2 to 20 distinct options for {field.Label}.";
            }
            else if (field.Options is { Count: > 0 } || (field.Type == "section" && field.Required))
                return "Options are only supported for choice questions. Sections cannot be required.";
        }
        return null;
    }

    private static string? ValidateAnswers(FormField[] fields, Dictionary<Guid, JsonElement> answers, bool submit)
    {
        var questions = fields.Where(field => field.Type != "section").ToDictionary(field => field.Id);
        if (answers.Keys.Any(id => !questions.ContainsKey(id))) return "An answer refers to a field outside this form.";
        foreach (var field in questions.Values)
        {
            if (!answers.TryGetValue(field.Id, out var answer) || answer.ValueKind == JsonValueKind.Null ||
                (answer.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(answer.GetString())) ||
                (answer.ValueKind == JsonValueKind.Array && answer.GetArrayLength() == 0))
            {
                if (submit && field.Required) return $"Answer {field.Label} before submitting.";
                continue;
            }
            if (!ValidAnswer(field, answer)) return $"Check the answer to {field.Label}.";
        }
        return null;
    }

    private static bool ValidAnswer(FormField field, JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return field.Type switch
        {
            "text" => text is { Length: <= 500 },
            "paragraph" => text is { Length: <= 4000 },
            "email" => text is { Length: <= 254 } && new EmailAddressAttribute().IsValid(text),
            "date" => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "time" => TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
            "rating" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var rating) && rating is >= 1 and <= 5,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "single" => text is not null && field.Options!.Contains(text),
            "multiple" => value.ValueKind == JsonValueKind.Array && value.GetArrayLength() <= field.Options!.Count &&
                value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String && field.Options.Contains(item.GetString()!)) &&
                value.EnumerateArray().Select(item => item.GetString()).Distinct().Count() == value.GetArrayLength(),
            _ => false
        };
    }

    public sealed record FormField(Guid Id, string Type, string Label, bool Required, IReadOnlyList<string>? Options);
    public sealed record FormInput([Required, StringLength(200)] string Name, [Required, StringLength(30)] string Kind,
        [Required] IReadOnlyList<FormField> Fields, Guid? ExpectedVersion, AudienceInput? Audience = null);
    public sealed record AudienceInput([Required, StringLength(20)] string Mode, [Required, MaxLength(100)] string[] Roles);
    public sealed record AudienceUpdate(Guid ExpectedVersion, [Required] AudienceInput Audience);
    public sealed record PublicAnswerInput([Required] Dictionary<Guid, JsonElement> Answers);
    public sealed record VersionInput(Guid ExpectedVersion);
    public sealed record AnswerInput([Required] Dictionary<Guid, JsonElement> Answers, Guid? ExpectedVersion, bool Submit);
}
