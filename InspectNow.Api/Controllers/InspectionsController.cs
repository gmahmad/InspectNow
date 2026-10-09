using InspectNow.Api.Contracts.Inspections;
using InspectNow.Application.Inspections;
using Microsoft.AspNetCore.Mvc;

namespace InspectNow.Api.Controllers;

[ApiController]
[Route("api/inspections")]
public sealed class InspectionsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(InspectionPageResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InspectionPageResult>> List(
        [FromQuery] ListInspectionsRequest request,
        [FromServices] ListInspections useCase,
        CancellationToken cancellationToken)
    {
        return Ok(await useCase.ExecuteAsync(request.ToQuery(), cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(StartInspectionResponse),
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
    public async Task<ActionResult<StartInspectionResponse>> Start(
        [FromBody] StartInspectionRequest request,
        [FromServices] StartInspection useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            request.TemplateId,
            request.SiteName,
            cancellationToken);

        switch (result.Outcome)
        {
            case StartInspectionOutcome.Started:
                return CreatedAtAction(
                    nameof(GetById),
                    new { id = result.InspectionId!.Value },
                    new StartInspectionResponse(
                        result.InspectionId!.Value));

            case StartInspectionOutcome.TemplateNotFound:
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Inspection template not found.");

            case StartInspectionOutcome.TemplateNotPublished:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The template is not published.",
                    detail: "Select a published template to start an inspection.");

            default:
                throw new InvalidOperationException(
                    "Unexpected start-inspection outcome.");
        }
    }

    [HttpGet("{id:long:min(1)}")]
    [ProducesResponseType(
    typeof(InspectionDetailsResult),
    StatusCodes.Status200OK)]
    [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InspectionDetailsResult>> GetById(
    long id,
    [FromServices] GetInspection useCase,
    CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            id,
            cancellationToken);

        if (result is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Inspection not found.");
        }

        return Ok(result);
    }


    [HttpPut("{id:long:min(1)}/questions/{questionId:long:min(1)}/answer")]
    [ProducesResponseType(typeof(SaveInspectionAnswerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SaveInspectionAnswerResponse>> SaveAnswer(
        long id, long questionId,
        [FromBody] SaveInspectionAnswerRequest request,
        [FromServices] SaveInspectionAnswer useCase,
        CancellationToken cancellationToken)
    {
        // ApiController validation guarantees Answer is supplied and valid.
        var result = await useCase.ExecuteAsync(id, questionId, request.Answer!.Value,
            request.Comment, request.ExpectedVersion, cancellationToken);

        return result.Outcome switch
        {
            SaveInspectionAnswerOutcome.Saved => Ok(
                new SaveInspectionAnswerResponse(result.Version!.Value)),
            SaveInspectionAnswerOutcome.InspectionNotFound => Problem(
                statusCode: 404, title: "Inspection not found."),
            SaveInspectionAnswerOutcome.QuestionNotFound => Problem(
                statusCode: 404, title: "Question not found in this inspection."),
            SaveInspectionAnswerOutcome.InspectionNotDraft => Problem(
                statusCode: 409, title: "Only draft inspections can be changed."),
            SaveInspectionAnswerOutcome.ConcurrencyConflict => InspectionConflict(),
            SaveInspectionAnswerOutcome.InvalidAnswer => Problem(
                statusCode: 400, title: "The answer is invalid.", detail: result.Error),
            _ => throw new InvalidOperationException("Unexpected save-answer outcome.")
        };
    }

    [HttpPost("{id:long:min(1)}/submit")]
    [ProducesResponseType(typeof(SubmitInspectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitInspectionResponse>> Submit(
        long id,
        [FromBody] SubmitInspectionRequest request,
        [FromServices] SubmitInspection useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(id, request.ExpectedVersion, cancellationToken);
        return result.Outcome switch
        {
            SubmitInspectionOutcome.Submitted => Ok(new SubmitInspectionResponse(
                id, "Submitted", result.SubmittedAtUtc!.Value, result.Version!.Value)),
            SubmitInspectionOutcome.InspectionNotFound => Problem(
                statusCode: 404, title: "Inspection not found."),
            SubmitInspectionOutcome.InspectionNotDraft => Problem(
                statusCode: 409, title: "Only draft inspections can be submitted."),
            SubmitInspectionOutcome.RequiredAnswersMissing => Problem(
                statusCode: 409, title: "Required answers are missing.",
                detail: "Answer every required question with Pass or Fail before submitting."),
            SubmitInspectionOutcome.ConcurrencyConflict => InspectionConflict(),
            SubmitInspectionOutcome.InvalidVersion => Problem(
                statusCode: 400, title: "The inspection version is required."),
            _ => throw new InvalidOperationException("Unexpected submit-inspection outcome.")
        };
    }

    private ObjectResult InspectionConflict() => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "The inspection changed while you were editing it.",
        detail: "Reload the inspection, review the latest answers, and try again.");
}
