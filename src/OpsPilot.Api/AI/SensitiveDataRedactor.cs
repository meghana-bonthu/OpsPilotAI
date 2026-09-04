using System.Text.RegularExpressions;

namespace OpsPilot.Api.AI;

public sealed partial class SensitiveDataRedactor
    : ISensitiveDataRedactor
{
    public string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = EmailRegex()
            .Replace(value, "[REDACTED_EMAIL]");

        redacted = PhoneRegex()
            .Replace(redacted, "[REDACTED_PHONE]");

        redacted = SocialSecurityNumberRegex()
            .Replace(redacted, "[REDACTED_SSN]");

        return redacted;
    }

    [GeneratedRegex(
        @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(
        @"(?<!\d)(?:\+?1[\s.-]?)?(?:\(\d{3}\)|\d{3})[\s.-]?\d{3}[\s.-]?\d{4}(?!\d)")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(
        @"(?<!\d)\d{3}-\d{2}-\d{4}(?!\d)")]
    private static partial Regex SocialSecurityNumberRegex();
}
