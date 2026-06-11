namespace IRMSGen.Contracts.Wizard;

public enum WizardStep
{
    ProjectDefinition = 1,
    OutputStructure = 2,
    DatabaseConnection = 3,
    DataObjects = 4,
    DtoDesigner = 5,
    ApiDesigner = 6,
    ErrorDesigner = 7,
    ExternalServices = 8,
    Review = 9,
    SourceControl = 10,
    Deployment = 11,
    Generate = 12
}
