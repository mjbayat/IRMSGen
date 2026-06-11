namespace IRMSGen.Contracts.ExternalServices;

public sealed class ExternalErrorMappingDefinition
{
    public int ExternalStatus { get; set; }

    public string ExternalCode { get; set; } = string.Empty;

    public int InternalStatus { get; set; }

    public string InternalCode { get; set; } = string.Empty;
}
