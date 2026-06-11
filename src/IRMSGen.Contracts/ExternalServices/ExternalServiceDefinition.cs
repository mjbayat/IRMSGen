namespace IRMSGen.Contracts.ExternalServices;

public sealed class ExternalServiceDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "REST";

    public string BaseUrl { get; set; } = string.Empty;

    public string Authentication { get; set; } = "None";

    public List<ExternalOperationDefinition> Operations { get; set; } = [];
}
