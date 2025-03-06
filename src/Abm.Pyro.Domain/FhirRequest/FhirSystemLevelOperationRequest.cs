using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.Validation;
using MediatR;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirRequest;

public record FhirSystemLevelOperationRequest(
        string RequestSchema,
        string Tenant,
        string OperationName,
        string RequestId,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers,
        DateTimeOffset TimeStamp)
    :FhirRequestBase(
            RequestSchema, 
            Tenant,
            RequestId,
            RequestPath,
            QueryString,
            Headers,
            HttpVerbId.Post,
            TimeStamp), 
        IRequest<FhirResourceResponse>, 
        IValidatable;