using Abm.Pyro.Application.FhirResponse;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirRequest;

public record FhirHistoryInstanceLevelRequest(
        string RequestSchema,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers, 
        string ResourceName, 
        string ResourceId,
        DateTimeOffset TimeStamp)
    :FhirResourceNameResourceIdRequestBase(
            RequestSchema, 
            RequestPath,
            QueryString,
            Headers, 
            ResourceName, 
            ResourceId,
            HttpVerbId.Get,
            TimeStamp),
        IRequest<FhirResourceResponse>, 
        IValidatable;