namespace IRMSGen.Domain.Projects;

public sealed class IRMSProject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string RootNamespace { get; set; } = string.Empty;

    public string TargetFramework { get; set; } = "net8.0";

    public ProjectArchitecture Architecture { get; set; } = ProjectArchitecture.FeatureBasedCleanArchitecture;

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
