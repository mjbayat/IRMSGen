namespace IRMSGen.Contracts.DataSources;

public sealed class DataObjectDefinition
{
    public string Kind { get; set; } = "Table";

    public string Schema { get; set; } = "public";

    public string DatabaseName { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public List<FieldMappingDefinition> Fields { get; set; } = [];
}
