namespace Abm.Pyro.Domain.Configuration;

public class FhirValidationSettings
{
    public const string SectionName = "FhirValidation";
    
    public Uri? ProfilePackageServiceUrl { get; init; }
    public Uri? TerminologyServiceUrl { get; init; }

    public bool ValidateOnCreate { get; init; } = false;
    
    public bool ValidateOnUpdate { get; init; } = false;

}