using Ibernia.Assessment.Api.Services;
using Microsoft.Extensions.Configuration;

namespace Ibernia.Assessment.Api.Tests;

public sealed class AiOptionsTests
{
    private static AiOptions FromValues(Dictionary<string, string?> values) =>
        AiOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [Fact]
    public void Defaults_apply_when_nothing_is_configured()
    {
        var options = FromValues([]);

        Assert.Null(options.ApiKey);
        Assert.Equal("gemini-3.8-flash", options.Model);
        Assert.Equal(30, options.TimeoutSeconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_values_are_treated_as_unset(string blank)
    {
        var options = FromValues(new()
        {
            ["AI_API_KEY"] = blank,
            ["AI_MODEL"] = blank,
            ["AI_TIMEOUT_SECONDS"] = blank,
        });

        Assert.Null(options.ApiKey);
        Assert.Equal(AiOptions.DefaultModel, options.Model);
        Assert.Equal(AiOptions.DefaultTimeoutSeconds, options.TimeoutSeconds);
    }

    [Fact]
    public void Configured_values_are_used()
    {
        var options = FromValues(new()
        {
            ["AI_API_KEY"] = " test-key ",
            ["AI_MODEL"] = "gemini-3.7-flash",
            ["AI_TIMEOUT_SECONDS"] = "45",
        });

        Assert.Equal("test-key", options.ApiKey);
        Assert.Equal("gemini-3.7-flash", options.Model);
        Assert.Equal(45, options.TimeoutSeconds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("301")]
    [InlineData("thirty")]
    public void Invalid_timeout_falls_back_to_the_default(string timeout)
    {
        var options = FromValues(new() { ["AI_TIMEOUT_SECONDS"] = timeout });

        Assert.Equal(AiOptions.DefaultTimeoutSeconds, options.TimeoutSeconds);
    }

    [Fact]
    public void ToString_does_not_expose_the_api_key()
    {
        var options = FromValues(new() { ["AI_API_KEY"] = "super-secret-key" });

        Assert.DoesNotContain("super-secret-key", options.ToString());
    }
}
