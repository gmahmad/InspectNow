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
}