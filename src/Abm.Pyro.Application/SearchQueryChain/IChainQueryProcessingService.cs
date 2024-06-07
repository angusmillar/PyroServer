using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.SearchQueryChain;

public interface IChainQueryProcessingService
{
  Task<ChainQueryProcessingOutcome> Process(FhirResourceTypeId resourceTypeContext, KeyValuePair<string, StringValues> parameter);
}
