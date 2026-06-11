
using System.Text.RegularExpressions;


namespace IRMSGen.Domain.Metadata
{
    public sealed class ServiceMetadata
    {
        public string ServiceName { get; init; } = string.Empty;

        public string TargetFramework { get; init; } = "net8.0";

        public string Architecture { get; init; } = "Clean Architecture";

        public string Database { get; init; } = "postgresql";

        public List<EntityMetadata> Entities { get; init; } = [];
    }

    public sealed class EntityMetadata
    {
        public string Name { get; init; } = string.Empty;

        public bool GenerateCrud { get; init; } = true;

        public List<FieldMetadata> Fields { get; init; } = [];

        public List<MethodMetadata> Methods { get; init; } = [];
    }

    public sealed class FieldMetadata
    {
        public string Name { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        public bool Required { get; init; }
    }

    public sealed class MethodMetadata
    {
        public string Name { get; init; } = string.Empty;

        public string HttpMethod { get; init; } = "GET";

        public string Route { get; init; } = string.Empty;

        public string OperationType { get; init; } = "Query";

        public string RequestDto { get; init; } = string.Empty;

        public string ResponseDto { get; init; } = string.Empty;

        public bool UsesPaging { get; init; }

        public bool UsesDatabase { get; init; } = true;

        public bool UsesExternalService { get; init; }

        public string ExternalServiceName { get; init; } = string.Empty;

        public bool UsesCache { get; init; }

        public bool UsesMessageLog { get; init; }

        public bool UsesEncryption { get; init; }
    }

    public static partial class MetadataValidator
    {
        private static readonly HashSet<string> SupportedTypes =
        [
            "Guid", "string", "int", "long", "decimal", "bool", "DateTime"
        ];

        private static readonly HashSet<string> SupportedHttpMethods =
        [
            "GET", "POST", "PUT", "PATCH", "DELETE"
        ];

        public static IReadOnlyList<string> Validate(ServiceMetadata metadata)
        {
            var errors = new List<string>();

            ValidateDottedIdentifier(metadata.ServiceName, "Service name", errors);

            if (metadata.TargetFramework != "net8.0")
            {
                errors.Add("Target framework must be net8.0 in this version.");
            }

            if (!string.Equals(metadata.Database, "postgresql", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Database must be postgresql in this version.");
            }

            if (metadata.Entities.Count == 0)
            {
                errors.Add("At least one entity is required.");
            }

            if (!metadata.Entities.Any(entity => entity.GenerateCrud))
            {
                errors.Add("At least one entity must enable CRUD generation.");
            }

            foreach (var entity in metadata.Entities)
            {
                ValidateIdentifier(entity.Name, "Entity name", errors);

                if (entity.Fields.Count == 0)
                {
                    errors.Add($"Entity '{entity.Name}' must include at least one field.");
                }

                if (!entity.Fields.Any(field => field.Name == "Id" && field.Type == "Guid"))
                {
                    errors.Add($"Entity '{entity.Name}' must include an Id field of type Guid.");
                }

                foreach (var field in entity.Fields)
                {
                    ValidateIdentifier(field.Name, $"Field name in '{entity.Name}'", errors);
                    if (!SupportedTypes.Contains(field.Type))
                    {
                        errors.Add($"Field '{entity.Name}.{field.Name}' uses unsupported type '{field.Type}'.");
                    }
                }

                var duplicateFields = entity.Fields
                    .GroupBy(field => field.Name, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key);
                errors.AddRange(duplicateFields.Select(name => $"Entity '{entity.Name}' has duplicate field '{name}'."));

                foreach (var method in entity.Methods)
                {
                    ValidateIdentifier(method.Name, $"Method name in '{entity.Name}'", errors);
                    if (!SupportedHttpMethods.Contains(method.HttpMethod.ToUpperInvariant()))
                    {
                        errors.Add($"Method '{entity.Name}.{method.Name}' uses unsupported HTTP method '{method.HttpMethod}'.");
                    }

                    if (method.OperationType is not ("Command" or "Query"))
                    {
                        errors.Add($"Method '{entity.Name}.{method.Name}' must be Command or Query.");
                    }

                    if (string.IsNullOrWhiteSpace(method.Route))
                    {
                        errors.Add($"Method '{entity.Name}.{method.Name}' must include a route.");
                    }

                    if (method.UsesExternalService)
                    {
                        ValidateIdentifier(
                            string.IsNullOrWhiteSpace(method.ExternalServiceName) ? "ExternalService" : method.ExternalServiceName,
                            $"External service name in '{entity.Name}.{method.Name}'",
                            errors);
                    }
                }

                var duplicateMethods = entity.Methods
                    .GroupBy(method => method.Name, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key);
                errors.AddRange(duplicateMethods.Select(name => $"Entity '{entity.Name}' has duplicate method '{name}'."));
            }

            var duplicateEntities = metadata.Entities
                .GroupBy(entity => entity.Name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);
            errors.AddRange(duplicateEntities.Select(name => $"Duplicate entity '{name}'."));

            return errors;
        }

        private static void ValidateIdentifier(string value, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value) || !IdentifierPattern().IsMatch(value))
            {
                errors.Add($"{label} '{value}' must be a valid C# identifier.");
            }
        }

        private static void ValidateDottedIdentifier(string value, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{label} '{value}' must be a valid C# namespace-style identifier.");
                return;
            }

            foreach (var part in value.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!IdentifierPattern().IsMatch(part))
                {
                    errors.Add($"{label} '{value}' must be a valid C# namespace-style identifier.");
                    return;
                }
            }

            if (value.Contains("..", StringComparison.Ordinal) || value.StartsWith('.') || value.EndsWith('.'))
            {
                errors.Add($"{label} '{value}' must be a valid C# namespace-style identifier.");
            }
        }

        [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
        private static partial Regex IdentifierPattern();
    }

}
