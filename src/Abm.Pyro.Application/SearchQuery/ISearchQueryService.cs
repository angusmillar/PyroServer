using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.SearchQuery;

namespace Abm.Pyro.Application.SearchQuery
{
  public interface ISearchQueryService
  {
    Task<SearchQueryServiceOutcome> Process(FhirResourceTypeId resourceTypeContext, string? queryString);
  }
}