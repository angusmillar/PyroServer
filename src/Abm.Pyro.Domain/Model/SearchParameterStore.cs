using Abm.Pyro.Domain.Enums;

#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class SearchParameterStore : DbBase
{
    private SearchParameterStore()
    {
    }

    public SearchParameterStore(
        int? searchParameterStoreId,
        string resourceId,
        int versionId,
        bool isCurrent,
        bool isDeleted,
        bool isIndexed,
        string name,
        PublicationStatusId status,
        Uri url,
        string code,
        SearchParamType type,
        List<SearchParameterStoreResourceTypeBase> baseList,
        List<SearchParameterStoreResourceTypeTarget> targetList,
        string? expression,
        bool? multipleOr,
        bool? multipleAnd,
        List<SearchParameterStoreComparator> comparatorList,
        List<SearchParameterStoreSearchModifierCode> modifierList,
        string? chain,
        List<SearchParameterStoreComponent> componentList,
        string json,
        DateTime lastUpdated,
        int rowVersion)
    {
        SearchParameterStoreId = searchParameterStoreId;
        ResourceId = resourceId;
        VersionId = versionId;
        IsCurrent = isCurrent;
        IsDeleted = isDeleted;
        IsIndexed = isIndexed;
        Name = name;
        Status = status;
        Url = url;
        Code = code;
        Type = type;
        BaseList = baseList;
        TargetList = targetList;
        Expression = expression;
        MultipleOr = multipleOr;
        MultipleAnd = multipleAnd;
        ComparatorList = comparatorList;
        ModifierList = modifierList;
        Chain = chain;
        ComponentList = componentList;
        Json = json;
        LastUpdated = lastUpdated;
        RowVersion = rowVersion;
    }

    public int? SearchParameterStoreId { get; set; }
    public string ResourceId { get; set; }
    public int VersionId { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsIndexed { get; set; }
    public string Name { get; set; }
    public PublicationStatusId Status { get; set; }
    public Uri Url { get; set; }

    public string Code { get; set; }

    public SearchParamType Type { get; set; }

    public List<SearchParameterStoreResourceTypeBase> BaseList { get; set; }

    public List<SearchParameterStoreResourceTypeTarget> TargetList { get; set; }

    public string? Expression { get; set; }

    public bool? MultipleOr { get; set; }

    public bool? MultipleAnd { get; set; }

    public List<SearchParameterStoreComparator> ComparatorList { get; set; }

    public List<SearchParameterStoreSearchModifierCode> ModifierList { get; set; }

    public string? Chain { get; set; }

    public List<SearchParameterStoreComponent> ComponentList { get; set; }

    public string Json { get; set; }

    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Optimistic concurrency Token
    /// https://docs.microsoft.com/en-us/aspnet/mvc/overview/getting-started/getting-started-with-ef-using-mvc/handling-concurrency-with-the-entity-framework-in-an-asp-net-mvc-application
    /// For SQLite: https://www.bricelam.net/2020/08/07/sqlite-and-efcore-concurrency-tokens.html
    /// </summary>
    public int RowVersion { get; set; }
}