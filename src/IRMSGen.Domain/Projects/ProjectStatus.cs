namespace IRMSGen.Domain.Projects;

public enum ProjectStatus
{
    Draft = 1,
    ReadyToGenerate = 2,
    Generated = 3,
    PublishedToGit = 4,
    Deployed = 5,
    Archived = 6
}
