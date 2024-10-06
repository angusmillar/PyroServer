using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirBundleService;

public interface IFhirTransactionGetService
{
    Task<OperationOutcome?> ProcessGets(
        string tenant,
        string requestId,
        List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        CancellationToken cancellationToken);

}