using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.FhirSupport;
using FhirUri = Abm.Pyro.Domain.FhirSupport.FhirUri;
namespace Abm.Pyro.Application.FhirResolver;

public class FhirPathResolve(IFhirUriFactory fhirUriFactory) : IFhirPathResolve
{
  public ITypedElement? Resolver(string url)
  {
    if (fhirUriFactory.TryParse(url, out FhirUri? fhirUri, out string _))
    {
        var defaultModelFactory = new Hl7.Fhir.Serialization.DefaultModelFactory();
        Type? type = ModelInfo.GetTypeForFhirType(fhirUri.ResourceName);
        if (type is null)
        {
          throw new ApplicationException($"ResourceName of '{fhirUri.ResourceName}' can not be converted to a FHIR Type.");
        }
        if (defaultModelFactory.Create(type) is DomainResource domainResource)
        {
          domainResource.Id = fhirUri.ResourceId;
          return domainResource.ToTypedElement().ToScopedNode();
        }
        throw new ApplicationException($"Unable to create a domain resource of type '{type.Name}'.");
    }
    return null;
  }
}
