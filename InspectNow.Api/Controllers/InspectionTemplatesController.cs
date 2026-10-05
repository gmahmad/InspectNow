using InspectNow.Api.Contracts.Templates;
using InspectNow.Application.Templates;
using Microsoft.AspNetCore.Mvc;

namespace InspectNow.Api.Controllers;

[ApiController]
[Route("api/inspection-templates")]
public sealed class InspectionTemplatesController : ControllerBase
{
    private readonly CreateInspectionTemplate _createTemplate;
    private readonly GetInspectionTemplate _getTemplate;

    public InspectionTemplatesController(
        CreateInspectionTemplate createTemplate,
        GetInspectionTemplate getTemplate)
    {
        _createTemplate = createTemplate;
        _getTemplate = getTemplate;
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(CreateTemplateResult),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateTemplateResult>> Create(
        [FromBody] CreateTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createTemplate.ExecuteAsync(
            request.Name,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(
        typeof(TemplateDetailsResult),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateDetailsResult>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _getTemplate.ExecuteAsync(
            id,
            cancellationToken);

        if (result is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Inspection template not found.");
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/questions")]
    [ProducesResponseType(
    typeof(AddTemplateQuestionResponse),
    StatusCodes.Status201Created)]
    [ProducesResponseType(
    typeof(ValidationProblemDetails),
    StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status404NotFound)]
    [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddTemplateQuestionResponse>> AddQuestion(
        Guid id,
        [FromBody] AddTemplateQuestionRequest request,
        [FromServices] AddTemplateQuestion useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            id,
            request.Text,
            request.IsRequired,
            cancellationToken);

        switch (result.Outcome)
        {
            case AddTemplateQuestionOutcome.Added:
                return StatusCode(
                    StatusCodes.Status201Created,
                    new AddTemplateQuestionResponse(
                        result.QuestionId!.Value));

            case AddTemplateQuestionOutcome.TemplateNotFound:
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Inspection template not found.");

            case AddTemplateQuestionOutcome.TemplateNotDraft:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Only draft templates can be modified.");

            case AddTemplateQuestionOutcome.ConcurrencyConflict:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The template changed while you were editing it.",
                    detail: "Reload the template and try again.");

            default:
                throw new InvalidOperationException(
                    "Unexpected add-question outcome.");
        }
    }
        [HttpPost("{id:guid}/publish")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status404NotFound)]
        [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Publish(
    Guid id,
    [FromServices] PublishInspectionTemplate useCase,
    CancellationToken cancellationToken)
        {
            var outcome = await useCase.ExecuteAsync(
                id,
                cancellationToken);

            switch (outcome)
            {
                case PublishTemplateOutcome.Published:
                    return NoContent();

                case PublishTemplateOutcome.TemplateNotFound:
                    return Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Inspection template not found.");

                case PublishTemplateOutcome.TemplateNotDraft:
                    return Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Only draft templates can be published.");

                case PublishTemplateOutcome.NoQuestions:
                    return Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "The template has no questions.",
                        detail: "Add at least one question before publishing.");

                case PublishTemplateOutcome.ConcurrencyConflict:
                    return Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "The template changed while publishing.",
                        detail: "Reload the template and review it before trying again.");

                default:
                    throw new InvalidOperationException(
                        "Unexpected publish outcome.");
            }
        }
    [HttpGet]
    [ProducesResponseType(
    typeof(TemplatePageResult),
    StatusCodes.Status200OK)]
    [ProducesResponseType(
    typeof(ValidationProblemDetails),
    StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TemplatePageResult>> List(
    [FromQuery] ListTemplatesRequest request,
    [FromServices] ListInspectionTemplates useCase,
    CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);

        return Ok(result);
    }
}