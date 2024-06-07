using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryHas
{
  public SearchQueryHas? ChildSearchQueryHas { get; set; }
  public FhirResourceTypeId TargetResourceForSearchQuery { get; set; }
  public SearchParameterProjection? BackReferenceSearchParameter { get; set; }
  public SearchQueryBase? SearchQuery { get; set; }
}
