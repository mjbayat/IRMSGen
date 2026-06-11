namespace IRMSGen.Contracts.DataSources;

public sealed class FieldMappingDefinition
{
    public string SourceName { get; set; } = string.Empty;

    public string PropertyName { get; set; } = string.Empty;

    public string DbType { get; set; } = string.Empty;

    public string ClrType { get; set; } = "string";

    public bool IsPrimaryKey { get; set; }

    public bool IsNullable { get; set; }

    public bool IncludeInEntity { get; set; } = true;
}
