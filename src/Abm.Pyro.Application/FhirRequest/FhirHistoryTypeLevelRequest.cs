using Abm.Pyro.Application.FhirResponse;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirRequest;

public record FhirHistoryTypeLevelRequest(
        string RequestSchema,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers, 
        string ResourceName,
        DateTimeOffset TimeStamp)
    :FhirResourceNameRequestBase(
            RequestSchema,
            RequestPath,
            QueryString,
            Headers, 
            ResourceName,
            HttpVerbId.Get,
            TimeStamp),
        IRequest<FhirResourceResponse>, 
        IValidatable;