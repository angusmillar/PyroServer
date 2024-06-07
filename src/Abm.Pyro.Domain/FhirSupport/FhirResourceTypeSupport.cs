using System.Net;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirSupport;

public class FhirResourceTypeSupport : IFhirResourceTypeSupport, IFhirResourceNameSupport
{

  private readonly string[] FhirResourceTypeHashSet = ModelInfo.SupportedResources.ToArray();

  public FhirResourceTypeId GetRequiredFhirResourceType(string resourceName)
  {
    if (Enum.TryParse(resourceName, out FhirResourceTypeId fhirResourceType))
    {
      return fhirResourceType;
    }
    throw new FhirFatalException(httpStatusCode: HttpStatusCode.InternalServerError, $"The FHIR resource name of '{resourceName}' is not a recognised FHIR resource type, ensure you have the correct casing.");
  }
  
  public FhirResourceTypeId? TryGetResourceType(string resourceName)
  {
    return Enum.TryParse(resourceName, out FhirResourceTypeId fhirResourceType) ? fhirResourceType : null;
  }
  
  public bool IsResourceTypeString(string value)
  {
    return FhirResourceTypeHashSet.Contains(value);
  }
  
}
