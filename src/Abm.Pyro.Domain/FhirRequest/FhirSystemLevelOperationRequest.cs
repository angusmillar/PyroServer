using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Dispatcher;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirRequest;

public record FhirSystemLevelOperationRequest(
        string RequestSchema,
        string Tenant,
        string RequestId,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers,
        string OperationName,
        Resource Resource, 
        DateTimeOffset TimeStamp)
    :FhirResourceRequestBase(
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