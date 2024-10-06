using Abm.Pyro.Application.FhirResponse;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirRequest;

public record FhirBatchOrTransactionRequest(
        string RequestSchema,
        string Tenant,
        string RequestId,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers, 
        Resource Resource,
        DateTimeOffset TimeStamp)
    : FhirResourceRequestBase(
            RequestSchema, 
            Tenant,
            RequestId,
            RequestPath,
            QueryString,
            Headers, 
            Resource, 
            HttpVerbId.Post, 
            TimeStamp), 
        IRequest<FhirResourceResponse>, 
        IValidatable;