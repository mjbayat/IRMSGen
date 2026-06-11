namespace IRMSGen.Contracts.Deployment;

public sealed class DeploymentDefinition
{
    public bool Enabled { get; set; }

    public string Registry { get; set; } = string.Empty;

    public string ImageName { get; set; } = string.Empty;

    public string KubernetesNamespace { get; set; } = "default";

    public string DeploymentType { get; set; } = "Helm";
}
