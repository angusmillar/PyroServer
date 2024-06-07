using System.Xml.Linq;
using FluentResults;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.FhirBundleService;

public class FhirNarrativeSupport(
    IFhirBundleCommonSupport fhirBundleCommonSupport
) : IFhirNarrativeSupport
{
    /// <summary>
    /// Update all references found in the Resource narratives
    /// </summary>
    public void UpdateAllReferences(Narrative? narrative,
        Dictionary<string, BundleEntryTransactionMetaData> bundleEntryTransactionMetaDataDictionary)
    {
        if (narrative is null)
        {
            return;
        }

        var xDoc = XElement.Parse(narrative.Div);

        //Find and update all <a href=""/> references
        List<XElement> linkList = xDoc.Descendants().Where(x => x.Name.LocalName == "a").ToList();
        foreach (var link in linkList)
        {
            var href = link.Attributes().FirstOrDefault(x => x.Name.LocalName == "href");
            if (href != null)
            {
                string? updatedReference = UpdatedReference(bundleEntryTransactionMetaDataDictionary, fromReference: href.Value);
                if (!string.IsNullOrWhiteSpace(updatedReference))
                {
                    href.Value = updatedReference;
                }
            }
        }

        //Find and update all <img src=""/> references
        List<XElement> linkListImg = xDoc.Descendants().Where(x => x.Name.LocalName == "img").ToList();
        foreach (var link in linkListImg)
        {
            var src = link.Attributes().FirstOrDefault(x => x.Name.LocalName == "src");
            if (src != null)
            {
                string? updatedReference = UpdatedReference(bundleEntryTransactionMetaDataDictionary, fromReference: src.Value);
                if (!string.IsNullOrWhiteSpace(updatedReference))
                {
                    src.Value = updatedReference;
                }
            }
        }

        narrative.Div = xDoc.ToString();
    }

    private string? UpdatedReference(Dictionary<string, BundleEntryTransactionMetaData> bundleEntryTransactionMetaDataDictionary,
        string fromReference)
    {
        Result<Domain.FhirSupport.FhirUri> fhirUriResult = fhirBundleCommonSupport.ParseFhirUri(fromReference.Trim());
        if (fhirUriResult.IsFailed)
        {
            return null;
        }

        BundleEntryTransactionMetaData? transactionResourceActionOutcome = bundleEntryTransactionMetaDataDictionary.Values.FirstOrDefault(x =>
            (x.ForFullUrl.ResourceName.Equals(fhirUriResult.Value.ResourceName) &&
             x.ForFullUrl.ResourceId.Equals(fhirUriResult.Value.ResourceId))
            ||
            (x.ForFullUrl.IsUrn &&
             x.ForFullUrl.ResourceId.Equals(x.ForFullUrl.Urn)));

        if (transactionResourceActionOutcome is null)
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(transactionResourceActionOutcome.ResourceUpdateInfo);

        return fhirBundleCommonSupport.SetResourceReference(
            resourceReferenceFhirUri: fhirUriResult.Value,
            resourceName: transactionResourceActionOutcome.ResourceUpdateInfo.ResourceName,
            resourceId: transactionResourceActionOutcome.ResourceUpdateInfo.NewResourceId,
            versionId: transactionResourceActionOutcome.ResourceUpdateInfo.NewVersionId);
    }
}