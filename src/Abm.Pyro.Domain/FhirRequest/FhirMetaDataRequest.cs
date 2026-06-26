using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.Validation;
using Abm.Pyro.Domain.Dispatcher;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirRequest;

public record FhirMetaDataRequest(
        string RequestSchema,
        string Tenant,
        string RequestId,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers,
        DateTimeOffset TimeStamp)
    :FhirRequestBase(
            RequestSchema, 
            Tenant,
            RequestId,
            RequestPath,
            QueryString,
            Headers,
            HttpVerbId.Get,
            TimeStamp), 
        IRequest<FhirResourceResponse>, 
        IValidatable;