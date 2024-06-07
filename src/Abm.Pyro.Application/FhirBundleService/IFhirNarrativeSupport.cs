using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.FhirBundleService;

public interface IFhirNarrativeSupport
{
    /// <summary>
    /// Update all references found in the Resource narratives
    /// </summary>
    void UpdateAllReferences(Narrative? narrative,
        Dictionary<string, BundleEntryTransactionMetaData> bundleEntryTransactionMetaDataDictionary);
}