using Ibernia.Assessment.Api.Services;
using Microsoft.Extensions.Configuration;

namespace Ibernia.Assessment.Api.Tests;

public sealed class AiOptionsTests
{
    private static Dictionary<string, string?> Valid() => new()
    {
        ["AI_API_KEY"] = "test-key-NOT-REAL",
        ["AI_MODEL"] = "test-model-a",
        ["AI_TIMEOUT_SECONDS"] = "45",
    };

    private static AiOptions FromValues(Dictionary<string, string?> values) =>
        AiOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [Fact]
    public void Configured_values_are_used_as_given()
    {
        var options = FromValues(Valid());

        Assert.Equal("test-key-NOT-REAL", options.ApiKey);
        Assert.Equal("test-model-a", options.Model);
        Assert.Equal(45, options.TimeoutSeconds);
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed()
    {
        var values = Valid();
        values["AI_MODEL"] = "  test-model-b \r";
        values["AI_TIMEOUT_SECONDS"] = " 20 ";

        var options = FromValues(values);

        Assert.Equal("test-model-b", options.Model);
        Assert.Equal(20, options.TimeoutSeconds);
    }

    [Theory]
    [InlineData("AI_API_KEY", null)]
    [InlineData("AI_API_KEY", "  ")]
    [InlineData("AI_MODEL", null)]
    [InlineData("AI_MODEL", "")]
    [InlineData("AI_TIMEOUT_SECONDS", null)]
    [InlineData("AI_TIMEOUT_SECONDS", "")]
    [InlineData("AI_TIMEOUT_SECONDS", "0")]
    [InlineData("AI_TIMEOUT_SECONDS", "-5")]
    [InlineData("AI_TIMEOUT_SECONDS", "301")]
    [InlineData("AI_TIMEOUT_SECONDS", "thirty")]
    [InlineData("AI_TIMEOUT_SECONDS", "2.5")]
    public void Missing_or_invalid_value_fails_and_names_the_variable(string name, string? value)
    {
        var values = Valid();
        if (value is null)
        {
            values.Remove(name);
        }
        else
        {
            values[name] = value;
        }

        var ex = Assert.Throws<InvalidOperationException>(() => FromValues(values));

        Assert.Contains(name, ex.Message);
    }

    [Fact]
    public void No_hidden_defaults_when_nothing_is_configured()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => FromValues([]));

        Assert.Contains("AI_API_KEY", ex.Message);
        Assert.Contains("AI_MODEL", ex.Message);
        Assert.Contains("AI_TIMEOUT_SECONDS", ex.Message);
    }

    [Fact]
    public void Errors_and_ToString_never_contain_the_api_key()
    {
        var values = Valid();
        values["AI_API_KEY"] = "super-secret-key-value";
        values["AI_MODEL"] = "";

        var ex = Assert.Throws<InvalidOperationException>(() => FromValues(values));
        values["AI_MODEL"] = "test-model-a";
        var options = FromValues(values);

        Assert.DoesNotContain("super-secret-key-value", ex.Message);
        Assert.DoesNotContain("super-secret-key-value", options.ToString());
    }
}
