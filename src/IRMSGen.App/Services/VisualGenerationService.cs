using IRMSGen.Domain.Metadata;
using IRMSGen.Generator.Generation;


namespace IRMSGen.App.Services
{
    public sealed class VisualGenerationService(IWebHostEnvironment environment)
    {
        public GenerationResult Generate(ServiceMetadata metadata)
        {
            var errors = MetadataValidator.Validate(metadata);
            if (errors.Count > 0)
            {
                return new GenerationResult(null, errors);
            }

            var outputRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "generated"));
            var outputPath = new ServiceGenerator().Generate(metadata, outputRoot);
            return new GenerationResult(outputPath, []);
        }
    }

    public sealed record GenerationResult(string? OutputPath, IReadOnlyList<string> Errors);

}
