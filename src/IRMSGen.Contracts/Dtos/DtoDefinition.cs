namespace IRMSGen.Contracts.Dtos;

public sealed class DtoDefinition
{
    public string Name { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string Kind { get; set; } = "Response";

    public List<DtoFieldDefinition> Fields { get; set; } = [];
}
