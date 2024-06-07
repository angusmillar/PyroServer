namespace Abm.Pyro.Domain.SearchQueryEntity;
public class SearchQueryCompositeValue(bool IsMissing) : SearchQueryValueBase(IsMissing)
{
  public List<SearchQueryBase> SearchQueryBaseList { get; set; } = new();
}
