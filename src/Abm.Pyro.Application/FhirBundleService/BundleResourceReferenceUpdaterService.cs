using System.Net;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Exceptions;
using FhirUri = Abm.Pyro.Domain.FhirSupport.FhirUri;

namespace Abm.Pyro.Application.FhirBundleService;

public class BundleResourceReferenceUpdaterService(
    IFhirBundleCommonSupport fhirBundleCommonSupport)
{
    public void Process(Bundle bundle)
    {
        foreach (var entry in bundle.Entry)
        {
            if (entry.Request is null)
            {
                throw new FhirErrorException(httpStatusCode: HttpStatusCode.BadRequest, "All Bundle.entry[x].request properties must be populated where Bundle.type = Transaction");
            }

            if (entry.Request.Method is Bundle.HTTPVerb.PUT)
            {
                FhirUri requestFhirUri = fhirBundleCommonSupport.ParseBundleRequestFhirUriOrThrow(entry);
                if (string.IsNullOrWhiteSpace(requestFhirUri.ResourceId))
                {
                    //It is a Conditional Update with a search string rather then ResourceId [base]/Patient?identifier=system|id
                }
            }
            
        }
        
    }
}