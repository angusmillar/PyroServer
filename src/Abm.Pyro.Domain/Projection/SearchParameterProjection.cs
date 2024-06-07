using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Projection;

public class SearchParameterProjection(
  int? searchParameterStoreId,
  string code,
  PublicationStatusId status,
  bool isCurrent,
  bool isDeleted,
  Uri url,
  SearchParamType type,
  string? expression,
  bool? multipleOr,
  bool? multipleAnd,
  List<SearchParameterStoreResourceTypeBase> baseList,
  List<SearchParameterStoreResourceTypeTarget> targetList,
  List<SearchParameterStoreComparator> comparatorList,
  List<SearchParameterStoreSearchModifierCode> modifierList,
  List<SearchParameterStoreComponent> componentList)
{
  public int? SearchParameterStoreId { get; init; } = searchParameterStoreId;

  public string Code { get; init; } = code;

  public PublicationStatusId Status { get; init; } = status;

  public bool IsCurrent { get; init; } = isCurrent;

  public bool IsDeleted { get; init; } = isDeleted;

  public Uri Url { get; init; } = url;

  public SearchParamType Type { get; init; } = type;

  public string? Expression  { get; init; } = expression;

  public bool? MultipleOr { get; init; } = multipleOr;

  public bool? MultipleAnd { get; init; } = multipleAnd;

  public List<SearchParameterStoreResourceTypeBase> BaseList { get; init; } = baseList;

  public List<SearchParameterStoreResourceTypeTarget> TargetList { get; init; } = targetList;

  public List<SearchParameterStoreComparator> ComparatorList { get; init; } = comparatorList;

  public List<SearchParameterStoreSearchModifierCode> ModifierList { get; init; } = modifierList;

  public List<SearchParameterStoreComponent> ComponentList { get; init; } = componentList;
}
