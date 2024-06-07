using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public interface ICommonRequestValidation
{
    IEnumerable<string> DoResourceNamesMatch(string requestEndpointResourceName, string requestBodyResourceResourceName);
    IEnumerable<string> IsRequestResourceIdPopulated(string? requestResourceId);
    IEnumerable<string> DoResourceIdsMatch(string requestEndpointResourceId, string requestBodyResourceResourceId);
    IEnumerable<string> IsValidRequestEndpointResourceType(string requestEndpointResourceName);
}