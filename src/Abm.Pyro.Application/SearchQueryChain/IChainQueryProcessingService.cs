using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.SearchQueryChain;

namespace Abm.Pyro.Application.SearchQueryChain;

public interface IChainQueryProcessingService
{
  Task<ChainQueryProcessingOutcome> Process(FhirResourceTypeId resourceTypeContext, KeyValuePair<string, StringValues> parameter);
}
