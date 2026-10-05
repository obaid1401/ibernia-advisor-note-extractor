using System.Text.Json.Nodes;

namespace Ibernia.Assessment.Api.Services;

public sealed record LlmRequest(string SystemInstruction, string UserContent, JsonObject ResponseSchema);

// Provider seam: returns the model's raw JSON text, or throws ExtractionException
// (Timeout, ProviderUnavailable, ProviderBusy, InvalidModelResponse).
public interface ILlmClient
{
    Task<string> GenerateJsonAsync(LlmRequest request, CancellationToken cancellationToken);
}
