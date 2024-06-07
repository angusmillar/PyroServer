using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Projection;

public class SearchParameterMetaDataProjection(
  int? searchParameterStoreId,
  string code,
  Uri url,
  SearchParamType type,
  List<SearchParameterStoreResourceTypeBase> baseList,
  List<SearchParameterStoreResourceTypeTarget> targetList)
{
  public int? SearchParameterStoreId { get; init; } = searchParameterStoreId;

  public string Code { get; init; } = code;
  
  public Uri Url { get; init; } = url;

  public SearchParamType Type { get; init; } = type;
  
  public List<SearchParameterStoreResourceTypeBase> BaseList { get; init; } = baseList;

  public List<SearchParameterStoreResourceTypeTarget> TargetList { get; init; } = targetList;
  
}
