using Abm.Pyro.Domain.Enums;

#pragma warning disable CS8618

namespace Abm.Pyro.Domain.Model;

public class ResourceType : DbBase
{
  private ResourceType() { }
  
  public ResourceType(FhirResourceTypeId fhirResourceType, string name)
  {
    FhirResourceType = fhirResourceType;
    Name = name;
  }
  
  public FhirResourceTypeId FhirResourceType { get; set; }
  public string Name { get; set; }
}
