namespace OpsPilot.Api.AI;

public interface ISensitiveDataRedactor
{
    string Redact(string value);
}
