namespace Abm.Pyro.Domain.Enums;

public enum FhirOperationLevel
{
    /// <summary>
    /// Fhir operations at the system level 
    /// </summary>
    System,
    /// <summary>
    /// Fhir operations at the Resource Type level 
    /// </summary>
    Type,
    /// <summary>
    /// Fhir operations at the Resource Instance level 
    /// </summary>
    Instance
}