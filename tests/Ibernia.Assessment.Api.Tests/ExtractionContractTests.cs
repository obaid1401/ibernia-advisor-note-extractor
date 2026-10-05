using Ibernia.Assessment.Api.Services;
using static Ibernia.Assessment.Api.Tests.ModelOutput;

namespace Ibernia.Assessment.Api.Tests;

// The broader extraction contract: how the four categories, missing values, currency/period and multiple
// facts come through the parser. The parser passes valid content through unchanged and adds nothing; whether
// the model avoids advice is governed by the prompt (see ExtractionPromptTests).
public sealed class ExtractionContractTests
{
    // Goals (A), future events (B), risks/questions (C): none, one, or many items, returned as given.
    [Theory]
    [InlineData("goals", 0)]
    [InlineData("goals", 1)]
    [InlineData("goals", 3)]
    [InlineData("futureEvents", 1)]
    [InlineData("futureEvents", 3)]
    [InlineData("risksOrQuestions", 1)]
    [InlineData("risksOrQuestions", 3)]
    public void String_lists_return_zero_one_or_many_items_unchanged(string property, int count)
    {
        var items = Enumerable.Range(1, count).Select(i => $"{property} item {i}").ToArray();
        var json = Response(
            goals: property == "goals" ? items : null,
            futureEvents: property == "futureEvents" ? items : null,
            risksOrQuestions: property == "risksOrQuestions" ? items : null);

        var result = ExtractionResponseParser.Parse(json, "Synthetic notes.");

        var list = property switch
        {
            "goals" => result.Goals,
            "futureEvents" => result.FutureEvents,
            _ => result.RisksOrQuestions,
        };
        Assert.Equal(items, list);
        Assert.Empty(result.FinancialFacts);
        Assert.Empty(result.Warnings);
    }

    // A: a goal that mentions money stays a goal; the money is a separate, grounded fact.
    [Fact]
    public void Goals_stay_separate_from_financial_facts()
    {
        const string notes = "Priya wants to retire at 60. She has a pension of £300,000.";
        var json = Response(
            goals: ["Retire at 60"],
            facts: [Fact("pension", "Pension", 300000, "GBP", null, false, "a pension of £300,000")]);

        var result = ExtractionResponseParser.Parse(json, notes);

        Assert.Equal(["Retire at 60"], result.Goals);
        var fact = Assert.Single(result.FinancialFacts);
        Assert.Equal("pension", fact.Category);
    }

    // B: tentative wording remains an event and does not become a financial fact.
    [Fact]
    public void Tentative_event_remains_an_event_and_creates_no_fact()
    {
        const string notes = "He may sell his second property in five years.";
        var json = Response(futureEvents: ["May sell second property in five years"]);

        var result = ExtractionResponseParser.Parse(json, notes);

        Assert.Equal(["May sell second property in five years"], result.FutureEvents);
        Assert.Empty(result.FinancialFacts);
    }

    // C: a question from the notes is returned under risksOrQuestions exactly as recorded; nothing is added.
    [Fact]
    public void Question_is_returned_under_risks_or_questions_without_added_content()
    {
        const string notes = "Client asked: should I move my ISA into a pension?";
        var json = Response(risksOrQuestions: ["Should the client move the ISA into a pension?"]);

        var result = ExtractionResponseParser.Parse(json, notes);

        Assert.Equal(["Should the client move the ISA into a pension?"], result.RisksOrQuestions);
        Assert.Empty(result.Goals);
        Assert.Empty(result.FutureEvents);
        Assert.Empty(result.FinancialFacts);
        Assert.Empty(result.Warnings);
    }

    // D: notes with only a goal produce empty lists for everything else.
    [Fact]
    public void Notes_with_only_a_goal_produce_empty_other_categories()
    {
        var result = ExtractionResponseParser.Parse(
            Response(goals: ["Retire at 60"]), "Client wants to retire at 60.");

        Assert.Equal(["Retire at 60"], result.Goals);
        Assert.Empty(result.FinancialFacts);
        Assert.Empty(result.FutureEvents);
        Assert.Empty(result.RisksOrQuestions);
        Assert.Empty(result.Warnings);
    }

    // D: missing values stay null — no default amount, no default GBP, no default period.
    [Fact]
    public void Missing_amount_currency_and_period_stay_null()
    {
        const string notes = "They own a house worth 300,000 and also have a mortgage.";
        var json = Response(facts:
        [
            Fact("property", "House value", 300000, null, null, false, "a house worth 300,000"),
            Fact("debt", "Mortgage", null, null, null, false, "have a mortgage"),
        ]);

        var result = ExtractionResponseParser.Parse(json, notes);

        Assert.Equal(2, result.FinancialFacts.Count);
        var house = result.FinancialFacts[0];
        Assert.Equal(300000m, house.Amount);
        Assert.Null(house.Currency);
        Assert.Null(house.Period);
        var mortgage = result.FinancialFacts[1];
        Assert.Null(mortgage.Amount);
        Assert.Null(mortgage.Currency);
        Assert.Null(mortgage.Period);
    }

    // F: two facts of the same category are both kept, in order.
    [Fact]
    public void Multiple_facts_of_the_same_category_are_all_kept()
    {
        const string notes = "Their home is worth £450,000 and the buy-to-let flat is worth £200,000.";
        var json = Response(facts:
        [
            Fact("property", "Home", 450000, "GBP", null, false, "Their home is worth £450,000"),
            Fact("property", "Buy-to-let flat", 200000, "GBP", null, false, "the buy-to-let flat is worth £200,000"),
        ]);

        var result = ExtractionResponseParser.Parse(json, notes);

        Assert.Equal(["Home", "Buy-to-let flat"], result.FinancialFacts.Select(f => f.Label));
        Assert.Equal([450000m, 200000m], result.FinancialFacts.Select(f => f.Amount!.Value));
    }

    // G: stated currency and period are preserved; unstated ones stay null.
    [Theory]
    [InlineData("GBP", "annual", 65000, "She earns £65,000 a year")]
    [InlineData("EUR", null, 250000, "a flat in Spain worth €250,000")]
    [InlineData("USD", "monthly", 3000, "rental income of $3,000 a month")]
    [InlineData(null, null, 20000, "savings of 20,000")]
    public void Stated_currency_and_period_are_preserved_and_unstated_ones_stay_null(
        string? currency, string? period, int amount, string sourceText)
    {
        var json = Response(facts: [Fact("other", "Fact", amount, currency, period, false, sourceText)]);

        var fact = Assert.Single(ExtractionResponseParser.Parse(json, $"Notes: {sourceText}.").FinancialFacts);

        Assert.Equal(currency, fact.Currency);
        Assert.Equal(period, fact.Period);
        Assert.Equal((decimal)amount, fact.Amount);
    }

    // J: a realistic mixed note — all four categories together, nothing invented.
    [Fact]
    public void Mixed_note_returns_all_four_categories_together()
    {
        const string notes =
            "Sarah (45) wants to pay off her mortgage before she retires.\n" +
            "She earns £65,000 a year and has about £20k in savings.\n" +
            "She might move to Spain in 2030.\n" +
            "She asked whether she should keep paying into her workplace pension.";
        var json = Response(
            goals: ["Pay off mortgage before retirement"],
            facts:
            [
                Fact("income", "Salary", 65000, "GBP", "annual", false, "She earns £65,000 a year"),
                Fact("savings", "Savings", 20000, "GBP", null, true, "about £20k in savings"),
            ],
            futureEvents: ["Possible move to Spain in 2030"],
            risksOrQuestions: ["Whether to keep paying into the workplace pension"]);

        var result = ExtractionResponseParser.Parse(json, notes);

        Assert.Equal(["Pay off mortgage before retirement"], result.Goals);
        Assert.Equal(["Possible move to Spain in 2030"], result.FutureEvents);
        Assert.Equal(["Whether to keep paying into the workplace pension"], result.RisksOrQuestions);
        Assert.Empty(result.Warnings);

        // The mortgage balance is not stated, so no mortgage fact or amount appears.
        Assert.Equal(["income", "savings"], result.FinancialFacts.Select(f => f.Category));
        Assert.Equal(65000m, result.FinancialFacts[0].Amount);
        Assert.Equal("annual", result.FinancialFacts[0].Period);
        Assert.True(result.FinancialFacts[1].IsApproximate);
        Assert.Null(result.FinancialFacts[1].Period);
    }
}
