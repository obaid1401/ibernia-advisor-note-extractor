namespace Ibernia.Assessment.Api.Models;

// Nullable so that a missing, empty, or whitespace value reaches the controller's own validation
// (one consistent ProblemDetails message) instead of ASP.NET's implicit [Required] check.
public sealed record ExtractNoteRequest(string? Notes);
