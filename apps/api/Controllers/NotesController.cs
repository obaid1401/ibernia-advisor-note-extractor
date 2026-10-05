using System.Diagnostics;
using Ibernia.Assessment.Api.Models;
using Ibernia.Assessment.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ibernia.Assessment.Api.Controllers;

[ApiController]
[Route("api/notes")]
public sealed class NotesController(INoteExtractionService extractionService, ILogger<NotesController> logger)
    : ControllerBase
{
    public const int MaxNotesLength = 10_000;
    public const int MaxRequestBodyBytes = 64 * 1024;

    [HttpPost("extract")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<ActionResult<ExtractedNote>> Extract(
        [FromBody] ExtractNoteRequest request,
        CancellationToken cancellationToken)
    {
        var notes = request.Notes?.Trim();
        if (string.IsNullOrEmpty(notes))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid notes",
                detail: "Please enter meeting notes.");
        }

        if (notes.Length > MaxNotesLength)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid notes",
                detail: "Notes must be 10,000 characters or fewer.");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await extractionService.ExtractAsync(notes, cancellationToken);
            logger.LogInformation(
                "Extraction succeeded for {NotesLength} characters in {ElapsedMs} ms.",
                notes.Length,
                stopwatch.ElapsedMilliseconds);
            return Ok(result);
        }
        catch (ExtractionException ex)
        {
            // The exception message is never logged or returned: only the failure kind.
            logger.LogWarning(
                "Extraction failed ({FailureKind}) for {NotesLength} characters after {ElapsedMs} ms.",
                ex.Kind,
                notes.Length,
                stopwatch.ElapsedMilliseconds);
            return ExtractionProblem(ex.Kind);
        }
    }

    private ObjectResult ExtractionProblem(ExtractionFailureKind kind)
    {
        var (status, detail) = kind switch
        {
            ExtractionFailureKind.InvalidModelResponse => (
                StatusCodes.Status502BadGateway,
                "The AI service returned an unexpected response. Please try again."),
            ExtractionFailureKind.ProviderBusy => (
                StatusCodes.Status503ServiceUnavailable,
                "The AI service is busy. Please wait a minute and try again."),
            ExtractionFailureKind.Timeout => (
                StatusCodes.Status504GatewayTimeout,
                "The AI service took too long. Please try again."),
            _ => (
                StatusCodes.Status503ServiceUnavailable,
                "The AI service is temporarily unavailable. Please try again."),
        };

        if (kind == ExtractionFailureKind.ProviderBusy)
        {
            Response.Headers.RetryAfter = "60";
        }

        return Problem(statusCode: status, title: "Extraction failed", detail: detail);
    }
}
