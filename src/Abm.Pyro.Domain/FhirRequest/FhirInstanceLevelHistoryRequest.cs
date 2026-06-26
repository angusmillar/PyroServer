using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.Validation;
using Abm.Pyro.Domain.Dispatcher;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirRequest;

public record FhirInstanceLevelHistoryRequest(
        string RequestSchema,
        string Tenant,
        string RequestId,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers, 
        string ResourceName, 
        string ResourceId,
        DateTimeOffset TimeStamp)
    :FhirResourceNameResourceIdRequestBase(
            RequestSchema, 
            Tenant,
            RequestId,
            RequestPath,
            QueryString,
            Headers, 
            ResourceName, 
            ResourceId,
            HttpVerbId.Get,
            TimeStamp),
        IRequest<FhirResourceResponse>, 
        IValidatable;