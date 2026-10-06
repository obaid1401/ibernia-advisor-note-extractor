using System.Net;
using System.Text;

namespace Ibernia.Assessment.Api.Tests.Fakes;

// Replaces the network for GeminiLlmClient tests and records the outgoing request.
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    : HttpMessageHandler
{
    public int Calls { get; private set; }
    public HttpRequestMessage? Request { get; private set; }
    public string? RequestBody { get; private set; }

    public static StubHttpMessageHandler Responding(HttpStatusCode statusCode, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Calls++;
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return await send(request, cancellationToken);
    }
}
