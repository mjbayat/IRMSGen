namespace IRMSGen.Domain.Generation;

public sealed class GenerationRun
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid ProjectId { get; init; }

    public int MetadataVersion { get; init; }

    public string OutputPath { get; set; } = string.Empty;

    public GenerationRunStatus Status { get; set; } = GenerationRunStatus.Pending;

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? FinishedAt { get; set; }
}
