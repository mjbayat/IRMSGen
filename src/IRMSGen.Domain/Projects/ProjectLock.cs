namespace IRMSGen.Domain.Projects;

public sealed class ProjectLock
{
    public Guid ProjectId { get; init; }

    public string LockedBy { get; init; } = string.Empty;

    public ProjectLockType Type { get; init; }

    public DateTimeOffset AcquiredAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; init; }
}
