namespace IRMSGen.Infrastructure.Persistence;

public sealed record PlatformProjectSummary(
    Guid Id,
    string Name,
    string RootNamespace,
    string TargetFramework,
    string Architecture,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PlatformConnectorSummary(
    Guid Id,
    string Name,
    string Type,
    string Status,
    string MetadataJson,
    DateTimeOffset UpdatedAt);
