namespace Ibernia.Assessment.Api.Models;

public sealed record ExtractedNote(
    IReadOnlyList<string> Goals,
    IReadOnlyList<FinancialFact> FinancialFacts,
    IReadOnlyList<string> FutureEvents,
    IReadOnlyList<string> RisksOrQuestions,
    IReadOnlyList<string> Warnings);
