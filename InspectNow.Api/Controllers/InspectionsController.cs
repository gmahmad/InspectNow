using InspectNow.Api.Contracts.Inspections;
using InspectNow.Application.Inspections;
using Microsoft.AspNetCore.Mvc;

namespace InspectNow.Api.Controllers;

[ApiController]
[Route("api/inspections")]
public sealed class InspectionsController : ControllerBase
{
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

}