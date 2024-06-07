using Abm.Pyro.Domain.Enums;

#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class IndexReference : IndexBase
{
  private IndexReference() : base() { }

  public IndexReference(int? indexReferenceId, int? resourceStoreId, ResourceStore? resourceStore, int? searchParameterStoreId, SearchParameterStore? searchParameterStore,
                        int? serviceBaseUrlId, ServiceBaseUrl? serviceBaseUrl, FhirResourceTypeId resourceType, string resourceId, string? versionId, string? canonicalVersionId)
    : base(resourceStoreId, resourceStore, searchParameterStoreId, searchParameterStore)
  {
    IndexReferenceId = indexReferenceId;
    ServiceBaseUrlId = serviceBaseUrlId;
    ServiceBaseUrl = serviceBaseUrl;
    ResourceType = resourceType;
    ResourceId = resourceId;
    VersionId = versionId;
    CanonicalVersionId = canonicalVersionId;
  }

  public int? IndexReferenceId { get; set; }
  public int? ServiceBaseUrlId { get; set; }
  public ServiceBaseUrl? ServiceBaseUrl { get; set; }
  public FhirResourceTypeId ResourceType { get; set; }
  public string ResourceId { get; set; }
  public string? VersionId { get; set; }
  public string? CanonicalVersionId { get; set; }
}
