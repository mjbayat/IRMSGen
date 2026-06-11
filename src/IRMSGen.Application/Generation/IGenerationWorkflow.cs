using IRMSGen.Domain.Metadata;

namespace IRMSGen.Application.Generation;

public interface IGenerationWorkflow
{
    Task<string> GenerateAsync(ServiceMetadata metadata, string outputPath, CancellationToken cancellationToken = default);
}
