using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ibernia.Assessment.Api.Models;

namespace Ibernia.Assessment.Api.Services;

// Pure and deterministic: validates the model's JSON against the extraction contract and checks every
// financial fact against the original notes. Structural problems reject the whole response; a fact that is
// not grounded in the notes is dropped and reported in Warnings. Never fills in missing data.
// Exception messages name only the offending field path, never values from the notes or the model.
public static partial class ExtractionResponseParser
{
    public const int MaxListItems = 20;
    public const int MaxFinancialFacts = 30;
    public const int MaxStringLength = 300;
    public const decimal MaxAmount = 1_000_000_000_000m;

    public const string SourceTextNotFoundWarning =
        "financial fact(s) removed because the quoted source text could not be found in the notes.";
    public const string AmountNotSupportedWarning =
        "financial fact(s) removed because the amount is not supported by the quoted source text.";

    public static ExtractedNote Parse(string? modelJson, string notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(modelJson ?? string.Empty);
        }
        catch (JsonException ex)
        {
            throw Invalid("Model output is not valid JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("Model output must be a JSON object.");
            }

            // Any "warnings" property from the model is deliberately ignored: warnings are server-generated.
            var goals = ReadStringList(root, "goals");
            var facts = ReadFinancialFacts(root);
            var futureEvents = ReadStringList(root, "futureEvents");
            var risksOrQuestions = ReadStringList(root, "risksOrQuestions");

            var (groundedFacts, warnings) = Ground(facts, notes);
            return new ExtractedNote(goals, groundedFacts, futureEvents, risksOrQuestions, warnings);
        }
    }

    // Collapses each run of whitespace (spaces, tabs, line breaks) to one space and trims. Nothing else changes.
    public static string NormaliseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    // An amount is supported only if it exactly equals a figure written in sourceText. Recognised figures:
    // digits with optional comma thousands separators and an optional decimal part ("420000", "420,000",
    // "2,500.50"), optionally followed by k / m / bn or thousand / million / billion ("£420k", "£1.5 million").
    // Surrounding symbols and words ("about £", "per year") do not matter. Anything not recognised - figures in
    // words, other separators, values the model calculated - fails closed and the fact is dropped.
    public static bool IsAmountSupported(decimal amount, string sourceText)
    {
        foreach (Match match in Figure().Matches(sourceText))
        {
            var digits = match.Groups["number"].Value.Replace(",", string.Empty);
            if (!decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            var multiplier = match.Groups["suffix"].Value.ToLowerInvariant() switch
            {
                "k" or "thousand" => 1_000m,
                "m" or "million" => 1_000_000m,
                "bn" or "billion" => 1_000_000_000m,
                _ => 1m,
            };

            // Figures above the maximum amount can never match; skipping them also avoids decimal overflow.
            if (value <= MaxAmount / multiplier && value * multiplier == amount)
            {
                return true;
            }
        }

        return false;
    }

    private static (IReadOnlyList<FinancialFact> Facts, IReadOnlyList<string> Warnings) Ground(
        IReadOnlyList<FinancialFact> facts,
        string notes)
    {
        var normalisedNotes = NormaliseWhitespace(notes);
        var grounded = new List<FinancialFact>();
        var notFound = 0;
        var amountNotSupported = 0;

        foreach (var fact in facts)
        {
            if (!normalisedNotes.Contains(NormaliseWhitespace(fact.SourceText), StringComparison.Ordinal))
            {
                notFound++;
            }
            else if (fact.Amount is { } amount && !IsAmountSupported(amount, fact.SourceText))
            {
                amountNotSupported++;
            }
            else
            {
                grounded.Add(fact);
            }
        }

        var warnings = new List<string>();
        if (notFound > 0)
        {
            warnings.Add($"{notFound} {SourceTextNotFoundWarning}");
        }

        if (amountNotSupported > 0)
        {
            warnings.Add($"{amountNotSupported} {AmountNotSupportedWarning}");
        }

        return (grounded, warnings);
    }

    private static IReadOnlyList<FinancialFact> ReadFinancialFacts(JsonElement root)
    {
        var array = GetRequired(root, "financialFacts", "financialFacts");
        if (array.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("'financialFacts' must be an array.");
        }

        if (array.GetArrayLength() > MaxFinancialFacts)
        {
            throw Invalid($"'financialFacts' has more than {MaxFinancialFacts} items.");
        }

        var facts = new List<FinancialFact>();
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            facts.Add(ReadFinancialFact(item, $"financialFacts[{index}]"));
            index++;
        }

        return facts;
    }

    private static FinancialFact ReadFinancialFact(JsonElement item, string path)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw Invalid($"'{path}' must be an object.");
        }

        var category = ReadString(item, "category", path);
        if (!FinancialFact.Categories.Contains(category))
        {
            throw Invalid($"'{path}.category' is not an allowed category.");
        }

        var label = ReadString(item, "label", path);
        var amount = ReadAmount(item, path);

        var currency = ReadNullableString(item, "currency", path);
        if (currency is not null && !IsCurrencyCode(currency))
        {
            throw Invalid($"'{path}.currency' must be a three-letter upper-case ISO 4217 code or null.");
        }

        var period = ReadNullableString(item, "period", path);
        if (period is not null && !FinancialFact.Periods.Contains(period))
        {
            throw Invalid($"'{path}.period' is not an allowed period.");
        }

        var isApproximate = GetRequired(item, "isApproximate", path) switch
        {
            { ValueKind: JsonValueKind.True } => true,
            { ValueKind: JsonValueKind.False } => false,
            _ => throw Invalid($"'{path}.isApproximate' must be a boolean."),
        };

        var sourceText = ReadString(item, "sourceText", path);

        return new FinancialFact(category, label, amount, currency, period, isApproximate, sourceText);
    }

    private static decimal? ReadAmount(JsonElement item, string path)
    {
        var element = GetRequired(item, "amount", path);
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.Number:
                // TryGetDecimal fails for values outside decimal's range, so huge numbers are rejected too.
                if (!element.TryGetDecimal(out var amount) || amount < 0 || amount > MaxAmount)
                {
                    throw Invalid($"'{path}.amount' must be null or a number between 0 and {MaxAmount}.");
                }

                return amount;
            default:
                throw Invalid($"'{path}.amount' must be a number or null.");
        }
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement root, string name)
    {
        var array = GetRequired(root, name, name);
        if (array.ValueKind != JsonValueKind.Array)
        {
            throw Invalid($"'{name}' must be an array.");
        }

        if (array.GetArrayLength() > MaxListItems)
        {
            throw Invalid($"'{name}' has more than {MaxListItems} items.");
        }

        var values = new List<string>();
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            values.Add(ValidateString(item, $"{name}[{index}]"));
            index++;
        }

        return values;
    }

    private static string ReadString(JsonElement parent, string name, string path) =>
        ValidateString(GetRequired(parent, name, path), $"{path}.{name}");

    private static string? ReadNullableString(JsonElement parent, string name, string path)
    {
        var element = GetRequired(parent, name, path);
        return element.ValueKind == JsonValueKind.Null ? null : ValidateString(element, $"{path}.{name}");
    }

    private static string ValidateString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Invalid($"'{path}' must be a string.");
        }

        var value = element.GetString()!.Trim();
        if (value.Length == 0)
        {
            throw Invalid($"'{path}' must not be blank.");
        }

        if (value.Length > MaxStringLength)
        {
            throw Invalid($"'{path}' is longer than {MaxStringLength} characters.");
        }

        return value;
    }

    private static JsonElement GetRequired(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var element))
        {
            var fullPath = path == name ? name : $"{path}.{name}";
            throw Invalid($"Required property '{fullPath}' is missing.");
        }

        return element;
    }

    private static bool IsCurrencyCode(string value) => value.Length == 3 && value.All(char.IsAsciiLetterUpper);

    private static ExtractionException Invalid(string message, Exception? inner = null) =>
        new(ExtractionFailureKind.InvalidModelResponse, message, inner);

    // A figure must not start or end inside a longer number: "420,0000" and "4,20,000" yield no figure.
    [GeneratedRegex(
        @"(?<![\d.,])(?<number>\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)(?!\d|,\d)(?:\s?(?<suffix>k|m|bn|thousand|million|billion)\b)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Figure();
}
