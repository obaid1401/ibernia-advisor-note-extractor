using Ibernia.Assessment.Api.Models;

namespace Ibernia.Assessment.Api.Services;

public sealed class NoteExtractionService(ILlmClient llmClient, ILogger<NoteExtractionService> logger)
    : INoteExtractionService
{
    public async Task<ExtractedNote> ExtractAsync(string notes, CancellationToken cancellationToken)
    {
        var request = new LlmRequest(
            ExtractionPrompt.SystemInstruction,
            ExtractionPrompt.BuildUserContent(notes),
            ExtractionPrompt.CreateResponseSchema());

        var modelJson = await llmClient.GenerateJsonAsync(request, cancellationToken);
        var result = ExtractionResponseParser.Parse(modelJson, notes);

        // Counts only: never note text or extracted values.
        logger.LogInformation(
            "Extracted {GoalCount} goals, {FactCount} financial facts, {EventCount} future events, {RiskCount} risks/questions; {WarningCount} grounding warnings.",
            result.Goals.Count,
            result.FinancialFacts.Count,
            result.FutureEvents.Count,
            result.RisksOrQuestions.Count,
            result.Warnings.Count);

        return result;
    }
}
