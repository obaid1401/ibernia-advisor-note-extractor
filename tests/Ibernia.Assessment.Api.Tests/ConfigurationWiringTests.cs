using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Ibernia.Assessment.Api.Services;
using Ibernia.Assessment.Api.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Ibernia.Assessment.Api.Tests;

// Uses the real Program.cs wiring (AiOptions, typed HttpClient, GeminiLlmClient); only the network is stubbed.
public sealed class ConfigurationWiringTests
{
    private const string ApiKey = "wiring-key-NOT-REAL";
    private const string Model = "configured-model-x1";

    private static WebApplicationFactory<Program> App(
        StubHttpMessageHandler network,
        string? apiKey = ApiKey,
        string? model = Model,
        string? timeoutSeconds = "30") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Empty string = not configured (also overrides any value set on the machine).
            builder.UseSetting("AI_API_KEY", apiKey ?? string.Empty);
            builder.UseSetting("AI_MODEL", model ?? string.Empty);
            builder.UseSetting("AI_TIMEOUT_SECONDS", timeoutSeconds ?? string.Empty);
            builder.ConfigureTestServices(services =>
                services.AddHttpClient<ILlmClient, GeminiLlmClient>().ConfigurePrimaryHttpMessageHandler(() => network));
        });

    private static string Envelope() => new JsonObject
    {
        ["candidates"] = new JsonArray(new JsonObject
        {
            ["content"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject
                {
                    ["text"] = """{"goals":["Retire at 60"],"financialFacts":[],"futureEvents":[],"risksOrQuestions":[]}""",
                }),
            },
            ["finishReason"] = "STOP",
        }),
    }.ToJsonString();

    [Fact]
    public async Task Configured_model_and_key_are_used_for_the_gemini_request()
    {
        var network = StubHttpMessageHandler.Responding(HttpStatusCode.OK, Envelope());
        using var app = App(network);

        var response = await app.CreateClient().PostAsJsonAsync("/api/notes/extract", new { notes = "Wants to retire at 60." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent",
            network.Request!.RequestUri!.ToString());
        Assert.Equal([ApiKey], network.Request.Headers.GetValues("x-goog-api-key"));
    }

    [Fact]
    public async Task Configured_timeout_is_used()
    {
        var network = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });
        using var app = App(network, timeoutSeconds: "1");
        var client = app.CreateClient();
        var stopwatch = Stopwatch.StartNew();

        var response = await client.PostAsJsonAsync("/api/notes/extract", new { notes = "Wants to retire at 60." });

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(0.9), TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(null, Model, "30", "AI_API_KEY")]
    [InlineData(ApiKey, null, "30", "AI_MODEL")]
    [InlineData(ApiKey, Model, null, "AI_TIMEOUT_SECONDS")]
    [InlineData(ApiKey, Model, "0", "AI_TIMEOUT_SECONDS")]
    [InlineData(ApiKey, Model, "abc", "AI_TIMEOUT_SECONDS")]
    public void App_does_not_start_with_missing_or_invalid_ai_configuration(
        string? apiKey,
        string? model,
        string? timeoutSeconds,
        string expectedVariable)
    {
        var network = StubHttpMessageHandler.Responding(HttpStatusCode.OK, Envelope());
        using var app = App(network, apiKey, model, timeoutSeconds);

        var ex = Assert.Throws<InvalidOperationException>(() => app.CreateClient());

        Assert.Contains(expectedVariable, ex.Message);
        Assert.DoesNotContain(ApiKey, ex.Message);
        Assert.Equal(0, network.Calls);
    }
}
