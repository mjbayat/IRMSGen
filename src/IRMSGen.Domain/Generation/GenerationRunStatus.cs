namespace IRMSGen.Domain.Generation;

public enum GenerationRunStatus
{
    Pending = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5
}
