using Microsoft.Extensions.Primitives;
using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class SearchQueryValidator(
    IOperationOutcomeSupport operationOutcomeSupport,
    IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport)
    : ValidatorBase<SearchQueryServiceOutcomeAndHeaders>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(SearchQueryServiceOutcomeAndHeaders item)
    {
        if (item.searchQueryServiceOutcome.HasInvalidQuery)
        {
            FailureMessageList.AddRange(item.searchQueryServiceOutcome.InvalidSearchQueryMessageList());
        }
        
        if (item.searchQueryServiceOutcome.HasUnsupportedQuery && fhirRequestHttpHeaderSupport.GetPreferHandling(item.headers) == PreferHandlingType.Strict)
        {
            FailureMessageList.AddRange(item.searchQueryServiceOutcome.UnsupportedQueryMessageList());
        }
        
        return GetValidatorResult();
    }
}

public record SearchQueryServiceOutcomeAndHeaders(SearchQueryServiceOutcome searchQueryServiceOutcome, Dictionary<string, StringValues> headers) : IValidatable;
