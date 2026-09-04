using OpsPilot.Api.AI;

namespace OpsPilot.Api.Tests;

public sealed class SensitiveDataRedactorTests
{
    private readonly SensitiveDataRedactor _redactor = new();

    [Fact]
    public void Redact_ReplacesEmailAddress()
    {
        var result = _redactor.Redact(
            "Contact user@example.com for details.");

        Assert.DoesNotContain(
            "user@example.com",
            result);

        Assert.Contains(
            "[REDACTED_EMAIL]",
            result);
    }

    [Fact]
    public void Redact_ReplacesPhoneNumber()
    {
        var result = _redactor.Redact(
            "Call 816-555-1234 immediately.");

        Assert.DoesNotContain(
            "816-555-1234",
            result);

        Assert.Contains(
            "[REDACTED_PHONE]",
            result);
    }

    [Fact]
    public void Redact_ReplacesSocialSecurityNumber()
    {
        var result = _redactor.Redact(
            "SSN: 123-45-6789");

        Assert.DoesNotContain(
            "123-45-6789",
            result);

        Assert.Contains(
            "[REDACTED_SSN]",
            result);
    }

    [Fact]
    public void Redact_LeavesOrdinaryTextUnchanged()
    {
        const string value =
            "Database server is unavailable.";

        var result = _redactor.Redact(value);

        Assert.Equal(
            value,
            result);
    }
}
