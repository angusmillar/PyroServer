using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirBundleService;

public interface IFhirTransactionDeleteService
{
    Task<OperationOutcome?> PreProcessDeletes(
        List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, BundleEntryTransactionMetaData> bundleEntryTransactionMetaDataDictionary,
        CancellationToken cancellationToken);

    Task ProcessDelete(
        List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, BundleEntryTransactionMetaData> bundleEntryTransactionMetaDataDictionary,
        CancellationToken cancellationToken);
}