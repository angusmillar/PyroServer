using System.Globalization;
using System.Net;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirSupport;

public class FhirBundleCreationCreationSupport(
  IFhirDeSerializationSupport fhirDeSerializationSupport,
  IServiceBaseUrlCache serviceBaseUrlCache) : IFhirBundleCreationSupport
{
  private ServiceBaseUrl? PrimaryServiceBaseUrl;

  public async Task<Bundle> CreateBundle(ResourceStoreSearchOutcome resourceStoreSearchOutcome, Bundle.BundleType bundleType, string requestSchema)
  {
    PrimaryServiceBaseUrl = await serviceBaseUrlCache.GetRequiredPrimaryAsync();
    var fhirBundle = new Bundle
    {
      Id = GuidSupport.NewFhirGuid(), 
      Type = bundleType,
      Total = resourceStoreSearchOutcome.SearchTotal
    };

    foreach (ResourceStore resourceStore in resourceStoreSearchOutcome.ResourceStoreList)
    {
      fhirBundle.Entry.Add(CreateEntry(resourceStore, bundleType, requestSchema, Bundle.SearchEntryMode.Match));
    }
    foreach (ResourceStore resourceStore in resourceStoreSearchOutcome.IncludedResourceStoreList)
    { 
      fhirBundle.Entry.Add(CreateEntry(resourceStore, bundleType, requestSchema, Bundle.SearchEntryMode.Include));
    }
    return fhirBundle;
  }
  private Bundle.EntryComponent CreateEntry(ResourceStore resourceStore, Bundle.BundleType bundleType, string requestSchema, Bundle.SearchEntryMode searchEntryMode)
  {

    if (PrimaryServiceBaseUrl is null)
    {
      throw new NullReferenceException(nameof(PrimaryServiceBaseUrl));
    }
    
    Bundle.EntryComponent oResEntry = new Bundle.EntryComponent();
    if (resourceStore.IsDeleted == false)
    {
      try
      {
        oResEntry.Resource = fhirDeSerializationSupport.ToResource(resourceStore.Json);
      }
      catch (Exception oExec)
      {
        string message = $"Serialization of a Resource retrieved from the servers database failed. The record's details were, Key: {resourceStore.ResourceStoreId}, Received: {resourceStore.LastUpdatedUtc.ToString(CultureInfo.CurrentCulture)}. The parser exception error was '{oExec.Message}";
        throw new FhirFatalException(HttpStatusCode.InternalServerError, message);
      }
    }

    oResEntry.FullUrl = String.Join('/', $"{requestSchema}:/", PrimaryServiceBaseUrl.Url, resourceStore.ResourceType.GetCode(), resourceStore.ResourceId);

    if (bundleType == Bundle.BundleType.History)
    {
      oResEntry.Request = new Bundle.RequestComponent();
      oResEntry.Request.Url = string.Join("/", resourceStore.ResourceType.GetCode(), resourceStore.ResourceId, "_history", resourceStore.VersionId);
      switch (resourceStore.HttpVerb)
      {
        case HttpVerbId.Post:
          oResEntry.Request.Method = Bundle.HTTPVerb.POST;
          break;
        case HttpVerbId.Put:
          oResEntry.Request.Method = Bundle.HTTPVerb.PUT;
          break;
        case HttpVerbId.Get:
          oResEntry.Request.Method = Bundle.HTTPVerb.GET;
          break;
        case HttpVerbId.Delete:
          oResEntry.Request.Method = Bundle.HTTPVerb.DELETE;
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(resourceStore.HttpVerb));
      }
    }

    if (bundleType == Bundle.BundleType.Searchset)
    {
      oResEntry.Search = new Bundle.SearchComponent();
      oResEntry.Search.Mode = searchEntryMode;
      oResEntry.Link = new List<Bundle.LinkComponent>();
    }
    return oResEntry;
  }
}
