using Abm.Pyro.Domain.Enums;

#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class ResourceStore : DbBase
{

  private ResourceStore()
  {
  }
  
  public ResourceStore(int? resourceStoreId, string resourceId, int versionId, FhirResourceTypeId resourceType, bool isCurrent, bool isDeleted, 
                       HttpVerbId httpVerb, string json, DateTime lastUpdatedUtc,
                       List<IndexReference> indexReferenceList, List<IndexString> indexStringList, List<IndexDateTime> indexDateTimeList, 
                       List<IndexQuantity> indexQuantityList, List<IndexToken> indexTokenList, List<IndexUri> indexUriList, 
                       int rowVersion)
  {
    ResourceStoreId = resourceStoreId;
    ResourceId = resourceId;
    VersionId = versionId;
    IsCurrent = isCurrent;
    IsDeleted = isDeleted;
    ResourceType = resourceType;
    HttpVerb = httpVerb;
    Json = json;
    LastUpdatedUtc = lastUpdatedUtc;
    IndexReferenceList = indexReferenceList;
    IndexStringList = indexStringList;
    IndexDateTimeList = indexDateTimeList;
    IndexQuantityList = indexQuantityList;
    IndexTokenList = indexTokenList;
    IndexUriList = indexUriList;
    RowVersion = rowVersion;
  }
  public int? ResourceStoreId { get; set; }
  public string ResourceId { get; set; }
  public int VersionId { get; set; }
  public FhirResourceTypeId ResourceType { get; set; }
  public bool IsCurrent { get; set; }
  public bool IsDeleted { get; set; }
  public HttpVerbId HttpVerb { get; set; }
  public string Json { get; set; }
  public DateTime LastUpdatedUtc { get; set; }
  public List<IndexReference> IndexReferenceList { get; set; }
  public List<IndexString> IndexStringList { get; set; }
  public List<IndexDateTime> IndexDateTimeList { get; set; }
  public List<IndexQuantity> IndexQuantityList { get; set; }
  public List<IndexToken> IndexTokenList { get; set; }
  public List<IndexUri> IndexUriList { get; set; }
  
  /// <summary>
  /// Optimistic concurrency Token
  /// https://docs.microsoft.com/en-us/aspnet/mvc/overview/getting-started/getting-started-with-ef-using-mvc/handling-concurrency-with-the-entity-framework-in-an-asp-net-mvc-application
  /// For SQLite: https://www.bricelam.net/2020/08/07/sqlite-and-efcore-concurrency-tokens.html
  /// </summary>
  public int RowVersion { get; set; }
  
}
