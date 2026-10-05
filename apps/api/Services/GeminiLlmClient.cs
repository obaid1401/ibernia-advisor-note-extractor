using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Ibernia.Assessment.Api.Services;

// Calls Gemini generateContent and returns the model's JSON text. It checks only the Gemini envelope;
// the extraction content is validated by ExtractionResponseParser.
// Never logs or throws request/response bodies, provider error messages, or the API key. The key is sent
// only in the x-goog-api-key header. The timeout is HttpClient.Timeout, configured in Program.cs.
public sealed partial class GeminiLlmClient(HttpClient httpClient, AiOptions options, ILogger<GeminiLlmClient> logger)
    : ILlmClient
{
    public const string BaseAddress = "https://generativelanguage.googleapis.com/";
    public const int MaxOutputTokens = 8192;

    public async Task<string> GenerateJsonAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            logger.LogError("Gemini request not sent: AI_API_KEY is not configured.");
            throw Unavailable("AI_API_KEY is not configured.");
        }

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{Uri.EscapeDataString(options.Model)}:generateContent")
        {
            Content = new StringContent(BuildBody(request).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        message.Headers.Add("x-goog-api-key", options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed. If the caller cancelled instead, the exception propagates unchanged.
            logger.LogWarning("Gemini request timed out after {TimeoutSeconds}s.", options.TimeoutSeconds);
            throw new ExtractionException(ExtractionFailureKind.Timeout, "Gemini request timed out.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Gemini request failed: {ExceptionType}.", ex.GetType().Name);
            throw Unavailable("Gemini request failed.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw ProviderError(response.StatusCode, body);
            }

            return ReadText(body);
        }
    }

    private static JsonObject BuildBody(LlmRequest request) => new()
    {
        ["systemInstruction"] = new JsonObject
        {
            ["parts"] = new JsonArray(new JsonObject { ["text"] = request.SystemInstruction }),
        },
        ["contents"] = new JsonArray(new JsonObject
        {
            ["role"] = "user",
            ["parts"] = new JsonArray(new JsonObject { ["text"] = request.UserContent }),
        }),
        ["generationConfig"] = new JsonObject
        {
            ["responseMimeType"] = "application/json",
            ["responseSchema"] = request.ResponseSchema.DeepClone(),
            ["maxOutputTokens"] = MaxOutputTokens,
            ["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = "low" },
        },
    };

    private ExtractionException ProviderError(HttpStatusCode statusCode, string body)
    {
        var status = (int)statusCode;
        var providerStatus = ReadErrorStatus(body);

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("Gemini rate limited the request: HTTP {StatusCode} {ProviderStatus}.", status, providerStatus);
            return new ExtractionException(ExtractionFailureKind.ProviderBusy, $"Gemini returned HTTP {status}.");
        }

        if (status >= 500)
        {
            logger.LogWarning("Gemini server error: HTTP {StatusCode} {ProviderStatus}.", status, providerStatus);
        }
        else
        {
            // 400/401/403/404 mean our request, key, or model name is wrong: operators need to act.
            logger.LogError(
                "Gemini rejected the request: HTTP {StatusCode} {ProviderStatus}. Check AI_API_KEY, AI_MODEL, and the request format.",
                status,
                providerStatus);
        }

        return Unavailable($"Gemini returned HTTP {status}.");
    }

    private string ReadText(string body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw InvalidEnvelope("Gemini response is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw InvalidEnvelope("Gemini response is not a JSON object.");
            }

            if (root.TryGetProperty("promptFeedback", out var feedback)
                && feedback.ValueKind == JsonValueKind.Object
                && feedback.TryGetProperty("blockReason", out var blockReason))
            {
                logger.LogWarning("Gemini blocked the prompt: {BlockReason}.", SafeCode(blockReason));
                throw InvalidEnvelope("Gemini blocked the prompt.");
            }

            if (!root.TryGetProperty("candidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0
                || candidates[0].ValueKind != JsonValueKind.Object)
            {
                throw InvalidEnvelope("Gemini response has no candidate.");
            }

            var candidate = candidates[0];
            var finishReason = candidate.TryGetProperty("finishReason", out var reason) ? SafeCode(reason) : "missing";
            if (finishReason != "STOP")
            {
                logger.LogWarning("Gemini did not finish normally: {FinishReason}.", finishReason);
                throw InvalidEnvelope("Gemini finish reason was not STOP.");
            }

            var text = new StringBuilder();
            if (candidate.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.Object
                && content.TryGetProperty("parts", out var parts)
                && parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray())
                {
                    var isThought = part.ValueKind == JsonValueKind.Object
                        && part.TryGetProperty("thought", out var thought)
                        && thought.ValueKind == JsonValueKind.True;
                    if (!isThought
                        && part.ValueKind == JsonValueKind.Object
                        && part.TryGetProperty("text", out var partText)
                        && partText.ValueKind == JsonValueKind.String)
                    {
                        text.Append(partText.GetString());
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(text.ToString()))
            {
                throw InvalidEnvelope("Gemini response has no text.");
            }

            LogUsage(root);
            return text.ToString();
        }
    }

    private void LogUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        logger.LogInformation(
            "Gemini responded ({Model}): prompt {PromptTokens}, output {OutputTokens}, thinking {ThinkingTokens}, total {TotalTokens} tokens.",
            options.Model,
            Count(usage, "promptTokenCount"),
            Count(usage, "candidatesTokenCount"),
            Count(usage, "thoughtsTokenCount"),
            Count(usage, "totalTokenCount"));
    }

    private static int? Count(JsonElement usage, string name) =>
        usage.TryGetProperty(name, out var value) && value.TryGetInt32(out var count) ? count : null;

    // Only short upper-case codes (e.g. RESOURCE_EXHAUSTED, SAFETY) are logged; anything else is replaced.
    private static string SafeCode(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && Code().IsMatch(value.GetString()!) ? value.GetString()! : "unrecognised";

    private static string ReadErrorStatus(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("status", out var status)
                    ? SafeCode(status)
                    : "unrecognised";
        }
        catch (JsonException)
        {
            return "unrecognised";
        }
    }

    private static ExtractionException Unavailable(string message) =>
        new(ExtractionFailureKind.ProviderUnavailable, message);

    private static ExtractionException InvalidEnvelope(string message) =>
        new(ExtractionFailureKind.InvalidModelResponse, message);

    [GeneratedRegex("^[A-Z][A-Z_]{0,63}$")]
    private static partial Regex Code();
}
