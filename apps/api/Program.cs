using Ibernia.Assessment.Api;
using Ibernia.Assessment.Api.Services;
using Microsoft.AspNetCore.Cors.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();

// Unhandled exceptions are logged by type only (UnhandledExceptionLogger); the framework's own log entry
// includes the exception message and stack trace, so it is switched off.
builder.Services.AddExceptionHandler<UnhandledExceptionLogger>();
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);

// Options are read from the final configuration (environment variables, appsettings, test overrides).
// The app does not load .env files; see docs/DESIGN.md for local development.
builder.Services.AddSingleton(sp => AiOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddHttpClient<ILlmClient, GeminiLlmClient>((sp, client) =>
{
    client.BaseAddress = new Uri(GeminiLlmClient.BaseAddress);
    client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<AiOptions>().TimeoutSeconds);
});
builder.Services.AddScoped<INoteExtractionService, NoteExtractionService>();

// CORS_ALLOWED_ORIGINS: comma-separated list (the deployed frontend). The Vite dev server is allowed in
// Development only. With no origins configured, no cross-origin requests are allowed.
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
{
    var origins = (configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();
    if (environment.IsDevelopment())
    {
        origins.Add("http://localhost:5173");
    }

    options.AddDefaultPolicy(policy => policy
        .WithOrigins([.. origins])
        .WithMethods("GET", "POST")
        .WithHeaders("Content-Type"));
});

var app = builder.Build();

// Fail at startup, not on the first request, if AI_API_KEY / AI_MODEL / AI_TIMEOUT_SECONDS are missing or invalid.
_ = app.Services.GetRequiredService<AiOptions>();

// Unhandled exceptions become a generic ProblemDetails with no exception details: 500, except for
// BadHttpRequestException, which keeps its own status (e.g. 413 when the body exceeds [RequestSizeLimit]).
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();
app.UseCors();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program { }
