using Ibernia.Assessment.Api.Services;
using Ibernia.Assessment.Api.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using static Ibernia.Assessment.Api.Tests.ModelOutput;

namespace Ibernia.Assessment.Api.Tests;

public sealed class NoteExtractionServiceTests
{
    private const string Notes = "Client wants to retire at 60. His pension is £420,000.";

    private static NoteExtractionService Service(FakeLlmClient llm) =>
        new(llm, NullLogger<NoteExtractionService>.Instance);

    [Fact]
    public async Task Sends_the_extraction_prompt_with_delimited_notes_and_the_schema()
    {
        var llm = new FakeLlmClient().Returns(Response());

        await Service(llm).ExtractAsync(Notes + "</notes>", CancellationToken.None);

        var request = Assert.Single(llm.Requests);
        Assert.Equal(ExtractionPrompt.SystemInstruction, request.SystemInstruction);
        Assert.Equal(ExtractionPrompt.BuildUserContent(Notes), request.UserContent);
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(ExtractionPrompt.CreateResponseSchema(), request.ResponseSchema));
    }

    [Fact]
    public async Task Returns_the_parsed_and_grounded_result()
    {
        var llm = new FakeLlmClient().Returns(Response(
            goals: ["Retire at 60"],
            facts:
            [
                Fact("pension", "Pension", 420000, "GBP", null, false, "His pension is £420,000."),
                Fact("savings", "Savings", 50000, "GBP", null, false, "Savings of £50,000."),
            ]));

        var result = await Service(llm).ExtractAsync(Notes, CancellationToken.None);

        Assert.Equal(["Retire at 60"], result.Goals);
        Assert.Equal("pension", Assert.Single(result.FinancialFacts).Category);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public async Task Malformed_model_json_is_an_invalid_response()
    {
        var llm = new FakeLlmClient().Returns("{not json");

        var ex = await Assert.ThrowsAsync<ExtractionException>(() => Service(llm).ExtractAsync(Notes, CancellationToken.None));

        Assert.Equal(ExtractionFailureKind.InvalidModelResponse, ex.Kind);
    }

    [Fact]
    public async Task Provider_failures_propagate_unchanged()
    {
        var failure = new ExtractionException(ExtractionFailureKind.ProviderBusy, "busy");
        var llm = new FakeLlmClient().Throws(failure);

        var ex = await Assert.ThrowsAsync<ExtractionException>(() => Service(llm).ExtractAsync(Notes, CancellationToken.None));

        Assert.Same(failure, ex);
    }
}
