using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.Validation;
using Abm.Pyro.Domain.Dispatcher;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirRequest;

public record FhirConditionalDeleteRequest(
        string RequestSchema,
        string Tenant,
        string RequestId,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers,
        string ResourceName,
        DateTimeOffset TimeStamp)
    : FhirResourceNameRequestBase(
            RequestSchema, 
            Tenant,
            RequestId,
            RequestPath,
            QueryString,
            Headers, 
            ResourceName,
            HttpVerbId.Delete,
            TimeStamp),
        IRequest<FhirOptionalResourceResponse>, 
        IValidatable;