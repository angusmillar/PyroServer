using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirRequest;

public record FhirTypeLevelOperationRequest(
        string RequestSchema,
        string Tenant,
        string RequestId,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers,
        string OperationName,
        string ResourceName, 
        Resource Resource, 
        DateTimeOffset TimeStamp)
    :FhirResourceNameResourceRequestBase(
            RequestSchema, 
            Tenant,
            RequestId,
            RequestPath,
            QueryString,
            Headers,
            ResourceName, 
            Resource,
            HttpVerbId.Post,
            TimeStamp), 
        IRequest<FhirResourceResponse>, 
        IValidatable;