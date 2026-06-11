namespace IRMSGen.Contracts.ExternalServices;

public sealed class ExternalOperationDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Method { get; set; } = "GET";

    public string Path { get; set; } = string.Empty;

    public string RequestModel { get; set; } = string.Empty;

    public string ResponseModel { get; set; } = string.Empty;

    public List<ExternalErrorMappingDefinition> ErrorMappings { get; set; } = [];
}
