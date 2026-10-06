using System.Net;
using System.Text.Json.Nodes;
using Ibernia.Assessment.Api.Services;
using Ibernia.Assessment.Api.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace Ibernia.Assessment.Api.Tests;

public sealed class GeminiLlmClientTests
{
    private const string ApiKey = "test-key-NOT-REAL-8c1f";
    private const string Model = "test-model-from-config";
    private const string Notes = "Synthetic client has a pension of £420,000.";
    private const string ModelJson = """{"goals":[],"financialFacts":[],"futureEvents":[],"risksOrQuestions":[]}""";

    private readonly CapturingLoggerProvider logs = new();

    private GeminiLlmClient Client(StubHttpMessageHandler handler, string apiKey = ApiKey, TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(GeminiLlmClient.BaseAddress),
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };
        var logger = LoggerFactory.Create(b => b.AddProvider(logs)).CreateLogger<GeminiLlmClient>();
        return new GeminiLlmClient(httpClient, new AiOptions { ApiKey = apiKey, Model = Model, TimeoutSeconds = 30 }, logger);
    }

    private static LlmRequest Request() => new(
        ExtractionPrompt.SystemInstruction,
        ExtractionPrompt.BuildUserContent(Notes),
        ExtractionPrompt.CreateResponseSchema());

    private static string Envelope(string text, string finishReason = "STOP") => new JsonObject
    {
        ["candidates"] = new JsonArray(new JsonObject
        {
            ["content"] = new JsonObject
            {
                ["role"] = "model",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = text }),
            },
            ["finishReason"] = finishReason,
        }),
        ["usageMetadata"] = new JsonObject
        {
            ["promptTokenCount"] = 900,
            ["candidatesTokenCount"] = 120,
            ["thoughtsTokenCount"] = 40,
            ["totalTokenCount"] = 1060,
        },
    }.ToJsonString();

    private async Task<ExtractionException> AssertFails(StubHttpMessageHandler handler, ExtractionFailureKind kind)
    {
        var ex = await Assert.ThrowsAsync<ExtractionException>(
            () => Client(handler).GenerateJsonAsync(Request(), CancellationToken.None));
        Assert.Equal(kind, ex.Kind);
        return ex;
    }

    [Fact]
    public async Task Successful_response_returns_the_model_json_text()
    {
        var handler = StubHttpMessageHandler.Responding(HttpStatusCode.OK, Envelope(ModelJson));

        var text = await Client(handler).GenerateJsonAsync(Request(), CancellationToken.None);

        Assert.Equal(ModelJson, text);
        Assert.Contains(logs.Entries, e => e.Contains("total 1060 tokens"));
    }

    [Fact]
    public async Task Request_targets_generate_content_with_the_key_only_in_the_header()
    {
        var handler = StubHttpMessageHandler.Responding(HttpStatusCode.OK, Envelope(ModelJson));

        await Client(handler).GenerateJsonAsync(Request(), CancellationToken.None);

        var request = handler.Request!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/test-model-from-config:generateContent",
            request.RequestUri!.ToString());
        Assert.Equal([ApiKey], request.Headers.GetValues("x-goog-api-key"));
        Assert.DoesNotContain(ApiKey, request.RequestUri.ToString());
        Assert.Empty(request.RequestUri.Query);
        Assert.DoesNotContain(ApiKey, handler.RequestBody);
        Assert.DoesNotContain(ApiKey, logs.AllText);
    }

    [Fact]
    public async Task Request_body_carries_prompt_schema_and_generation_config()
    {
        var handler = StubHttpMessageHandler.Responding(HttpStatusCode.OK, Envelope(ModelJson));

        await Client(handler).GenerateJsonAsync(Request(), CancellationToken.None);

        var body = JsonNode.Parse(handler.RequestBody!)!;
        Assert.Equal(ExtractionPrompt.SystemInstruction, (string)body["systemInstruction"]!["parts"]![0]!["text"]!);
        Assert.Equal("user", (string)body["contents"]![0]!["role"]!);
        Assert.Equal(ExtractionPrompt.BuildUserContent(Notes), (string)body["contents"]![0]!["parts"]![0]!["text"]!);

        var config = body["generationConfig"]!;
        Assert.Equal("application/json", (string)config["responseMimeType"]!);
        Assert.True(JsonNode.DeepEquals(ExtractionPrompt.CreateResponseSchema(), config["responseSchema"]));
        Assert.Equal("low", (string)config["thinkingConfig"]!["thinkingLevel"]!);
        Assert.Equal(GeminiLlmClient.MaxOutputTokens, (int)config["maxOutputTokens"]!);
        Assert.Null(config["temperature"]);
    }

    [Fact]
    public async Task Thought_parts_are_ignored_and_text_parts_are_joined()
    {
        var envelope = new JsonObject
        {
            ["candidates"] = new JsonArray(new JsonObject
            {
                ["content"] = new JsonObject
                {
                    ["parts"] = new JsonArray(
                        new JsonObject { ["text"] = "thinking...", ["thought"] = true },
                        new JsonObject { ["text"] = "{\"goals\":[]," },
                        new JsonObject { ["text"] = "\"rest\":1}" }),
                },
                ["finishReason"] = "STOP",
            }),
        }.ToJsonString();

        var text = await Client(StubHttpMessageHandler.Responding(HttpStatusCode.OK, envelope))
            .GenerateJsonAsync(Request(), CancellationToken.None);

        Assert.Equal("{\"goals\":[],\"rest\":1}", text);
    }

    [Fact]
    public async Task Missing_api_key_fails_without_sending_a_request()
    {
        var handler = StubHttpMessageHandler.Responding(HttpStatusCode.OK, Envelope(ModelJson));

        var ex = await Assert.ThrowsAsync<ExtractionException>(
            () => Client(handler, apiKey: "").GenerateJsonAsync(Request(), CancellationToken.None));

        Assert.Equal(ExtractionFailureKind.ProviderUnavailable, ex.Kind);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Timeout_maps_to_timeout()
    {
        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });

        var ex = await Assert.ThrowsAsync<ExtractionException>(() =>
            Client(handler, timeout: TimeSpan.FromMilliseconds(100)).GenerateJsonAsync(Request(), CancellationToken.None));

        Assert.Equal(ExtractionFailureKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_a_timeout()
    {
        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Client(handler).GenerateJsonAsync(Request(), cts.Token));
    }

    [Fact]
    public async Task Network_failure_maps_to_provider_unavailable()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("No such host is known. (generativelanguage.googleapis.com:443)"));

        await AssertFails(handler, ExtractionFailureKind.ProviderUnavailable);
    }

    [Fact]
    public async Task Rate_limit_maps_to_provider_busy()
    {
        var body = """{"error":{"code":429,"message":"Quota exceeded for metric","status":"RESOURCE_EXHAUSTED"}}""";

        await AssertFails(StubHttpMessageHandler.Responding(HttpStatusCode.TooManyRequests, body), ExtractionFailureKind.ProviderBusy);

        Assert.Contains(logs.Entries, e => e.Contains("RESOURCE_EXHAUSTED"));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Provider_errors_map_to_provider_unavailable(HttpStatusCode statusCode)
    {
        var body = """{"error":{"code":0,"message":"provider detail","status":"UNAVAILABLE"}}""";

        await AssertFails(StubHttpMessageHandler.Responding(statusCode, body), ExtractionFailureKind.ProviderUnavailable);
    }

    [Fact]
    public async Task Provider_error_messages_are_neither_thrown_nor_logged()
    {
        const string providerMessage = "API key not valid. Please pass a valid API key. SENSITIVE-PROVIDER-TEXT";
        var body = $$$"""{"error":{"code":400,"message":"{{{providerMessage}}}","status":"INVALID_ARGUMENT"}}""";

        var ex = await AssertFails(
            StubHttpMessageHandler.Responding(HttpStatusCode.BadRequest, body),
            ExtractionFailureKind.ProviderUnavailable);

        Assert.DoesNotContain("SENSITIVE-PROVIDER-TEXT", ex.Message);
        Assert.DoesNotContain("SENSITIVE-PROVIDER-TEXT", logs.AllText);
        Assert.Contains(logs.Entries, e => e.StartsWith("Error") && e.Contains("INVALID_ARGUMENT"));
    }

    [Fact]
    public async Task Blocked_prompt_is_an_invalid_response()
    {
        var body = """{"promptFeedback":{"blockReason":"SAFETY"}}""";

        await AssertFails(StubHttpMessageHandler.Responding(HttpStatusCode.OK, body), ExtractionFailureKind.InvalidModelResponse);

        Assert.Contains(logs.Entries, e => e.Contains("SAFETY"));
    }

    [Theory]
    [InlineData("MAX_TOKENS")]
    [InlineData("SAFETY")]
    [InlineData("RECITATION")]
    [InlineData("OTHER")]
    public async Task Non_stop_finish_reason_is_an_invalid_response(string finishReason) =>
        await AssertFails(
            StubHttpMessageHandler.Responding(HttpStatusCode.OK, Envelope(ModelJson, finishReason)),
            ExtractionFailureKind.InvalidModelResponse);

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"candidates":[]}""")]
    [InlineData("""{"candidates":["text"]}""")]
    [InlineData("""{"candidates":[{"finishReason":"STOP"}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[]},"finishReason":"STOP"}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"text":"  "}]},"finishReason":"STOP"}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"text":"{}"}]}}]}""")]
    public async Task Malformed_envelope_is_an_invalid_response(string body) =>
        await AssertFails(StubHttpMessageHandler.Responding(HttpStatusCode.OK, body), ExtractionFailureKind.InvalidModelResponse);

    [Fact]
    public async Task Malformed_model_json_is_returned_for_the_parser_to_reject()
    {
        // The client checks only the envelope; ExtractionResponseParser rejects the content (see endpoint tests).
        var handler = StubHttpMessageHandler.Responding(HttpStatusCode.OK, Envelope("{not valid json"));

        var text = await Client(handler).GenerateJsonAsync(Request(), CancellationToken.None);

        Assert.Equal("{not valid json", text);
        var ex = Assert.Throws<ExtractionException>(() => ExtractionResponseParser.Parse(text, Notes));
        Assert.Equal(ExtractionFailureKind.InvalidModelResponse, ex.Kind);
    }

    [Fact]
    public async Task Notes_and_model_output_are_not_logged()
    {
        var handler = StubHttpMessageHandler.Responding(HttpStatusCode.OK, Envelope("""{"marker":"MODEL-OUTPUT-TEXT"}"""));

        await Client(handler).GenerateJsonAsync(Request(), CancellationToken.None);

        Assert.DoesNotContain("£420,000", logs.AllText);
        Assert.DoesNotContain("MODEL-OUTPUT-TEXT", logs.AllText);
    }
}
