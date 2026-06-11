namespace IRMSGen.Contracts.Generation;

public sealed record GenerationOutputContract(
    string ServiceName,
    string OutputPath,
    IReadOnlyList<string> GeneratedFiles,
    IReadOnlyList<string> Warnings);
