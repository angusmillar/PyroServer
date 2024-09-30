using Abm.Pyro.Application.FhirResponse;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirRequest;

public record FhirVersionReadRequest(
        string RequestSchema,
        string tenant,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers, 
        string ResourceName, 
        string ResourceId,
        string HistoryId,
        DateTimeOffset TimeStamp)
    :FhirResourceNameResourceIdRequestBase(
            RequestSchema, 
            tenant,
            RequestPath,
            QueryString,
            Headers, 
            ResourceName, 
            ResourceId,
            HttpVerbId.Get,
            TimeStamp),
        IRequest<FhirOptionalResourceResponse>, 
        IValidatable;