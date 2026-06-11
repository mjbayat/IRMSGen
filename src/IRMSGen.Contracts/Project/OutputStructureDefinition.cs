namespace IRMSGen.Contracts.Project;

public sealed class OutputStructureDefinition
{
    public string Architecture { get; set; } = "FeatureBasedCleanArchitecture";

    public bool GenerateApiProject { get; set; } = true;

    public bool GenerateApplicationProject { get; set; } = true;

    public bool GenerateDomainProject { get; set; } = true;

    public bool GenerateInfrastructureProject { get; set; } = true;

    public bool GenerateContractsProject { get; set; } = true;

    public bool GenerateTests { get; set; } = true;

    public bool GenerateDocker { get; set; } = true;

    public bool GenerateKubernetes { get; set; } = true;

    public static OutputStructureDefinition CreateDefault() => new();
}
