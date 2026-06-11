namespace IRMSGen.Contracts.Errors;

public sealed class ErrorDefinition
{
    public string EndpointName { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public int HttpStatus { get; set; } = 400;

    public string Type { get; set; } = "Validation";

    public string Message { get; set; } = string.Empty;
}
