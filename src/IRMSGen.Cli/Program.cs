using System.Text.Json;
using IRMSGen.Domain.Metadata;
using IRMSGen.Generator.Generation;



if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintUsage();
    return 0;
}

if (!string.Equals(args[0], "generate", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Unknown command '{args[0]}'.");
    PrintUsage();
    return 1;
}

var inputPath = ReadOption(args, "--input");
var outputPath = ReadOption(args, "--output");

if (inputPath is null || outputPath is null)
{
    Console.Error.WriteLine("Both --input and --output are required.");
    PrintUsage();
    return 1;
}

try
{
    var json = await File.ReadAllTextAsync(inputPath);
    var metadata = JsonSerializer.Deserialize<ServiceMetadata>(
        json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Metadata file is empty.");

    var errors = MetadataValidator.Validate(metadata);
    if (errors.Count > 0)
    {
        Console.Error.WriteLine("Metadata is invalid:");
        foreach (var error in errors)
        {
            Console.Error.WriteLine($"- {error}");
        }

        return 1;
    }

    var generator = new ServiceGenerator();
    var generatedDirectory = generator.Generate(metadata, outputPath);
    Console.WriteLine($"Generated {metadata.ServiceName} in {generatedDirectory}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Generation failed: {exception.Message}");
    return 1;
}

static string? ReadOption(string[] arguments, string option)
{
    for (var index = 1; index < arguments.Length - 1; index++)
    {
        if (string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
        {
            return arguments[index + 1];
        }
    }

    return null;
}

static void PrintUsage()
{
    Console.WriteLine("IRMSGen - .NET 8 PostgreSQL microservice generator");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/IRMSGen.Cli -- generate --input <metadata.json> --output <directory>");
}
