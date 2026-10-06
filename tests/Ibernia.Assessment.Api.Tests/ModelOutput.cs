using System.Text.Json.Nodes;

namespace Ibernia.Assessment.Api.Tests;

// Builds model-output JSON for parser tests. All notes used with it are synthetic.
internal static class ModelOutput
{
    public static JsonObject Fact(
        string category,
        string label,
        decimal? amount,
        string? currency,
        string? period,
        bool isApproximate,
        string sourceText) => new()
    {
        ["category"] = category,
        ["label"] = label,
        ["amount"] = amount,
        ["currency"] = currency,
        ["period"] = period,
        ["isApproximate"] = isApproximate,
        ["sourceText"] = sourceText,
    };

    public static string Response(
        string[]? goals = null,
        JsonObject[]? facts = null,
        string[]? futureEvents = null,
        string[]? risksOrQuestions = null) => new JsonObject
    {
        ["goals"] = Strings(goals),
        ["financialFacts"] = new JsonArray([.. (facts ?? []).Select(f => (JsonNode)f)]),
        ["futureEvents"] = Strings(futureEvents),
        ["risksOrQuestions"] = Strings(risksOrQuestions),
    }.ToJsonString();

    private static JsonArray Strings(string[]? values) =>
        new([.. (values ?? []).Select(v => (JsonNode)JsonValue.Create(v))]);
}
