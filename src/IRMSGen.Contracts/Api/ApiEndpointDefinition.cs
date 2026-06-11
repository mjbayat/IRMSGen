namespace IRMSGen.Contracts.Api;

public sealed class ApiEndpointDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Feature { get; set; } = string.Empty;

    public string Method { get; set; } = "GET";

    public string Route { get; set; } = "/api/resource";

    public string OperationType { get; set; } = "Query";

    public string RequestDto { get; set; } = string.Empty;

    public string ResponseDto { get; set; } = string.Empty;

    public bool UsesPaging { get; set; }

    public bool UsesDatabase { get; set; } = true;

    public bool UsesExternalService { get; set; }

    public string ExternalServiceName { get; set; } = string.Empty;

    public bool UsesCache { get; set; }

    public bool UsesMessageLog { get; set; }

    public bool UsesEncryption { get; set; }
}
