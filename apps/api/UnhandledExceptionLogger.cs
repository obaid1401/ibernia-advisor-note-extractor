using Microsoft.AspNetCore.Diagnostics;

namespace Ibernia.Assessment.Api;

// Logs only the exception type for unhandled exceptions, because a message could contain request data.
// Returns false so the exception handler middleware still writes the generic 500 ProblemDetails.
// The middleware's own full-exception log is filtered out in Program.cs.
internal sealed class UnhandledExceptionLogger(ILogger<UnhandledExceptionLogger> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // A BadHttpRequestException (e.g. an oversized body) is a client error, not a server fault.
        logger.Log(
            exception is BadHttpRequestException ? LogLevel.Warning : LogLevel.Error,
            "Unhandled {ExceptionType} while processing {Method} {Path}.",
            exception.GetType().Name,
            httpContext.Request.Method,
            httpContext.Request.Path);
        return ValueTask.FromResult(false);
    }
}
