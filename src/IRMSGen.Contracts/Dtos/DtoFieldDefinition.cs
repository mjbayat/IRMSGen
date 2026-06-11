namespace IRMSGen.Contracts.Dtos;

public sealed class DtoFieldDefinition
{
    public string SourceField { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "string";

    public bool Required { get; set; }
}
