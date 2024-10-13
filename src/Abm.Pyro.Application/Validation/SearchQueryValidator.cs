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
        if (item.SearchQueryServiceOutcome.HasInvalidQuery)
        {
            FailureMessageList.AddRange(item.SearchQueryServiceOutcome.InvalidSearchQueryMessageList());
        }
        
        if (item.SearchQueryServiceOutcome.HasUnsupportedQuery && fhirRequestHttpHeaderSupport.GetPreferHandling(item.Headers) == PreferHandlingType.Strict)
        {
            FailureMessageList.AddRange(item.SearchQueryServiceOutcome.UnsupportedQueryMessageList());
        }
        
        return GetValidatorResult();
    }
}

public record SearchQueryServiceOutcomeAndHeaders(SearchQueryServiceOutcome SearchQueryServiceOutcome, Dictionary<string, StringValues> Headers) : IValidatable;
