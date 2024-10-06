using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirBundleService;

public interface IFhirTransactionPutService
{
    Task<OperationOutcome?> PreProcessPuts(List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, BundleEntryTransactionMetaData> bundleEntryTransactionMetaDataDictionary,
        CancellationToken cancellationToken);

    Task ProcessPuts(
        string tenant,
        string requestId,
        List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, BundleEntryTransactionMetaData> transactionResourceActionOutcomeDictionary,
        CancellationToken cancellationToken);
}