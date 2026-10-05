namespace Ibernia.Assessment.Api.Services;

// A class rather than a record: a record's generated ToString() would print the API key.
public sealed class AiOptions
{
    public const string DefaultModel = "gemini-3.8-flash";
    public const int DefaultTimeoutSeconds = 30;
    public const int MaxTimeoutSeconds = 300;

    public string? ApiKey { get; init; }
    public string Model { get; init; } = DefaultModel;
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;

    // Blank values are treated as unset, so an empty AI_MODEL (e.g. from docker-compose) keeps the default.
    public static AiOptions FromConfiguration(IConfiguration configuration)
    {
        var apiKey = configuration["AI_API_KEY"];
        var model = configuration["AI_MODEL"];
        var timeout = configuration["AI_TIMEOUT_SECONDS"];

        return new AiOptions
        {
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim(),
            Model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim(),
            TimeoutSeconds = int.TryParse(timeout, out var seconds) && seconds is > 0 and <= MaxTimeoutSeconds
                ? seconds
                : DefaultTimeoutSeconds,
        };
    }
}
