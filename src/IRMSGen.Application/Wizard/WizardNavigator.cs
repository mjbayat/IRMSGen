using IRMSGen.Contracts.Wizard;

namespace IRMSGen.Application.Wizard;

public static class WizardNavigator
{
    public static readonly WizardStep[] Steps =
    [
        WizardStep.ProjectDefinition,
        WizardStep.OutputStructure,
        WizardStep.DatabaseConnection,
        WizardStep.DataObjects,
        WizardStep.DtoDesigner,
        WizardStep.ApiDesigner,
        WizardStep.ErrorDesigner,
        WizardStep.ExternalServices,
        WizardStep.Review,
        WizardStep.SourceControl,
        WizardStep.Deployment,
        WizardStep.Generate
    ];

    public static WizardStep Next(WizardStep current)
    {
        var index = Array.IndexOf(Steps, current);
        return index < Steps.Length - 1 ? Steps[index + 1] : current;
    }

    public static WizardStep Previous(WizardStep current)
    {
        var index = Array.IndexOf(Steps, current);
        return index > 0 ? Steps[index - 1] : current;
    }
}
