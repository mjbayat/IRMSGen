using IRMSGen.Contracts.Api;
using IRMSGen.Contracts.DataSources;
using IRMSGen.Contracts.Deployment;
using IRMSGen.Contracts.Dtos;
using IRMSGen.Contracts.Errors;
using IRMSGen.Contracts.ExternalServices;
using IRMSGen.Contracts.Project;
using IRMSGen.Contracts.SourceControl;

namespace IRMSGen.Contracts.Wizard;

public sealed class WizardState
{
    public ProjectDefinition Project { get; set; } = ProjectDefinition.CreateDefault();

    public OutputStructureDefinition OutputStructure { get; set; } = OutputStructureDefinition.CreateDefault();

    public DatabaseConnectionDefinition DatabaseConnection { get; set; } = new();

    public List<ConnectorCredentialDefinition> ConnectorCredentials { get; set; } = [];

    public List<DataObjectDefinition> DataObjects { get; set; } = [];

    public List<DtoDefinition> Dtos { get; set; } = [];

    public List<ApiEndpointDefinition> ApiEndpoints { get; set; } = [];

    public List<ErrorDefinition> Errors { get; set; } = [];

    public List<ExternalServiceDefinition> ExternalServices { get; set; } = [];

    public SourceControlDefinition SourceControl { get; set; } = new();

    public DeploymentDefinition Deployment { get; set; } = new();

    public HashSet<string> InfrastructureConnectors { get; set; } = ["PostgreSQL"];

    public WizardStep CurrentStep { get; set; } = WizardStep.ProjectDefinition;

    public HashSet<WizardStep> CompletedSteps { get; set; } = [];
}
