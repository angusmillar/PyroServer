using Abm.Pyro.Application.FhirResponse;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirRequest;

public record FhirSearchRequest(
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
            HttpVerbId.Get,
            TimeStamp),
        IRequest<FhirResourceResponse>,
        IValidatable;
