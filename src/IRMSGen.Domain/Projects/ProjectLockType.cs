namespace IRMSGen.Domain.Projects;

public enum ProjectLockType
{
    Editing = 1,
    Generating = 2,
    PublishingToGit = 3,
    Deploying = 4
}
