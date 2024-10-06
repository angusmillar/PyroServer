using Abm.Pyro.Application.FhirResponse;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirRequest;

public record FhirCreateRequest(
        string RequestSchema,
        string Tenant,
        string RequestId,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers, 
        string ResourceName, 
        Resource Resource, 
        string? ResourceId,
        DateTimeOffset TimeStamp)
    : FhirResourceNameResourceNullableResourceIdRequestBase(
            RequestSchema, 
            Tenant,
            RequestId,
            RequestPath,
            QueryString,
            Headers, 
            ResourceName, 
            ResourceId, 
            Resource,
            HttpVerbId.Post,
            TimeStamp), 
        IRequest<FhirOptionalResourceResponse>, 
        IValidatable;