using System.Net;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class CommonRequestValidation(
    IFhirResourceNameSupport fhirResourceNameSupport
    ) : ICommonRequestValidation
{
    
    public IEnumerable<string> DoResourceNamesMatch(string requestEndpointResourceName, string requestBodyResourceResourceName)
    {
        if (requestEndpointResourceName.Equals(requestBodyResourceResourceName, StringComparison.Ordinal))
        {
            return Enumerable.Empty<string>();
        }
        
        return new[]
        {
            $"The resource type found in the body of the request did not match the endpoint's resource type it was submitted against. " +
            $"The resource in the body was of type '{requestBodyResourceResourceName}' yet the endpoint's resource type was '{requestEndpointResourceName}'. "
        };
        
    }
    
    public IEnumerable<string> IsRequestResourceIdPopulated(string? requestResourceId)
    {
        if (!String.IsNullOrWhiteSpace(requestResourceId))
        {
            return Enumerable.Empty<string>();
        }
    
        return new[]
        {
            "The request's resource's id property must be populated. Conditional deletes are not supported." 
        };
        
    }
    
    public IEnumerable<string> DoResourceIdsMatch(string requestEndpointResourceId, string requestBodyResourceResourceId)
    {
        if (requestEndpointResourceId.Equals(requestBodyResourceResourceId, StringComparison.Ordinal))
        {
            return Enumerable.Empty<string>();
        }
    
        return new[]
        {
            "The resource's id in the body must match the resource id in the URL" 
        };

    }
    
    public IEnumerable<string> IsValidRequestEndpointResourceType(string requestEndpointResourceName)
    {
        if (fhirResourceNameSupport.IsResourceTypeString(requestEndpointResourceName))
        {
            return Enumerable.Empty<string>();
        }
   
        return new[]
        {
            $"The endpoint's resource type of '{requestEndpointResourceName}' is not a known resource type. " 
        };
       
    }
}