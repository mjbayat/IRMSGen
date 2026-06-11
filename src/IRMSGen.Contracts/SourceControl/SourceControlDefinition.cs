namespace IRMSGen.Contracts.SourceControl;

public sealed class SourceControlDefinition
{
    public bool Enabled { get; set; }

    public string RepositoryUrl { get; set; } = string.Empty;

    public string Branch { get; set; } = "main";

    public bool CreatePullRequest { get; set; }
}
