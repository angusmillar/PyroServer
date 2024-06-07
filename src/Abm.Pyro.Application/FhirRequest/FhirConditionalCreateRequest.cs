using Abm.Pyro.Application.FhirResponse;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirRequest;

public record FhirConditionalCreateRequest(
        string RequestSchema,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers,
        string ResourceName,
        Resource Resource,
        DateTimeOffset TimeStamp)
    : FhirResourceNameResourceRequestBase(
            RequestSchema, 
            RequestPath,
            QueryString,
            Headers, 
            ResourceName, 
            Resource,
            HttpVerbId.Post,
            TimeStamp),
        IRequest<FhirOptionalResourceResponse>, 
        IValidatable;