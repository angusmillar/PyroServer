using Abm.Pyro.Domain.FhirBundleService;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirBundleService;

public interface IFhirTransactionPutAndPatchService
{
    Task<OperationOutcome?> PreProcessPutsAndPatches(List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, BundleEntryTransactionMetaData> bundleEntryTransactionMetaDataDictionary,
        CancellationToken cancellationToken);

    Task ProcessPutsAndPatches(
        string tenant,
        string requestId,
        List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, BundleEntryTransactionMetaData> transactionResourceActionOutcomeDictionary,
        CancellationToken cancellationToken);
}
