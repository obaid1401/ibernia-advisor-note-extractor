using System.Globalization;

namespace Ibernia.Assessment.Api.Services;

// A class rather than a record: a record's generated ToString() would print the API key.
public sealed class AiOptions
{
    // Safety limit, not a default: a configured timeout above this is rejected.
    public const int MaxTimeoutSeconds = 300;

    public required string ApiKey { get; init; }
    public required string Model { get; init; }
    public required int TimeoutSeconds { get; init; }

    // AI_API_KEY, AI_MODEL and AI_TIMEOUT_SECONDS are all required; nothing is defaulted. Missing or invalid
    // values throw, naming only the variables (never their values), so the app fails at startup instead of
    // silently using another model or timeout.
    public static AiOptions FromConfiguration(IConfiguration configuration)
    {
        var apiKey = configuration["AI_API_KEY"]?.Trim();
        var model = configuration["AI_MODEL"]?.Trim();
        var timeoutText = configuration["AI_TIMEOUT_SECONDS"]?.Trim();

        var problems = new List<string>();
        if (string.IsNullOrEmpty(apiKey))
        {
            problems.Add("AI_API_KEY is not set.");
        }

        if (string.IsNullOrEmpty(model))
        {
            problems.Add("AI_MODEL is not set.");
        }

        if (!int.TryParse(timeoutText, NumberStyles.None, CultureInfo.InvariantCulture, out var timeoutSeconds)
            || timeoutSeconds < 1
            || timeoutSeconds > MaxTimeoutSeconds)
        {
            problems.Add($"AI_TIMEOUT_SECONDS must be a whole number of seconds from 1 to {MaxTimeoutSeconds}.");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Invalid AI configuration: " + string.Join(" ", problems));
        }

        return new AiOptions { ApiKey = apiKey!, Model = model!, TimeoutSeconds = timeoutSeconds };
    }
}
