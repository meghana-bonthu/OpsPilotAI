namespace OpsPilot.Api.Domain;

public sealed class Team
{
    private Team() { }

    public Team(string name)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;
}