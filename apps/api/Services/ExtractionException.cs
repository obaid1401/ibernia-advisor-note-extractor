namespace Ibernia.Assessment.Api.Services;

public enum ExtractionFailureKind
{
    Timeout,
    ProviderUnavailable,
    ProviderBusy,
    InvalidModelResponse,
}

// Messages are for diagnostics only and must never contain note text or model output.
public sealed class ExtractionException(ExtractionFailureKind kind, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public ExtractionFailureKind Kind { get; } = kind;
}
