namespace Ibernia.Assessment.Api.Models;

public sealed record FinancialFact(
    string Category,
    string Label,
    decimal? Amount,
    string? Currency,
    string? Period,
    bool IsApproximate,
    string SourceText)
{
    public static readonly IReadOnlyList<string> Categories =
        ["pension", "savings", "investment", "property", "income", "spending", "debt", "other"];

    public static readonly IReadOnlyList<string> Periods = ["annual", "monthly"];
}
