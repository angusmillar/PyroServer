using Abm.Pyro.Application.FhirResponse;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirRequest;

public record FhirConditionalDeleteRequest(
        string RequestSchema,
        string tenant,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers,
        string ResourceName,
        DateTimeOffset TimeStamp)
    : FhirResourceNameRequestBase(
            RequestSchema, 
            tenant,
            RequestPath,
            QueryString,
            Headers, 
            ResourceName,
            HttpVerbId.Delete,
            TimeStamp),
        IRequest<FhirOptionalResourceResponse>, 
        IValidatable;