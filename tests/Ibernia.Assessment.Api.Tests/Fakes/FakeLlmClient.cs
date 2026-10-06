using Ibernia.Assessment.Api.Services;

namespace Ibernia.Assessment.Api.Tests.Fakes;

// Stands in for the Gemini client in service and endpoint tests; never touches the network.
internal sealed class FakeLlmClient : ILlmClient
{
    private Func<CancellationToken, Task<string>> respond = _ => Task.FromResult("{}");

    public List<LlmRequest> Requests { get; } = [];

    public FakeLlmClient Returns(string modelJson)
    {
        respond = _ => Task.FromResult(modelJson);
        return this;
    }

    public FakeLlmClient Throws(Exception exception)
    {
        respond = _ => Task.FromException<string>(exception);
        return this;
    }

    public Task<string> GenerateJsonAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return respond(cancellationToken);
    }
}
