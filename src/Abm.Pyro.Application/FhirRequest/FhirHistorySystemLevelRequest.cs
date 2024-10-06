using Abm.Pyro.Application.FhirResponse;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirRequest;

public record FhirHistorySystemLevelRequest(
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