﻿using Abm.Pyro.Application.FhirResponse;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirRequest;

public record FhirUpdateRequest(
        string RequestSchema,
        string Tenant,
        string RequestId,
        string RequestPath,
        string? QueryString,
        Dictionary<string, StringValues> Headers, 
        string ResourceName, 
        Resource Resource,
        string ResourceId,
        DateTimeOffset TimeStamp)
    : FhirResourceNameResourceRequestBase(
            RequestSchema,
            Tenant,
            RequestId,
            RequestPath,
            QueryString,
            Headers, 
            ResourceName, 
            Resource,
            HttpVerbId.Put,
            TimeStamp),
        IRequest<FhirOptionalResourceResponse>, 
        IValidatable;

public record SystemSubscriptionUpdateRequest(
    string Tenant,
    string RequestId,
    string ResourceName,
    Resource Resource,
    string ResourceId,
    DateTimeOffset TimeStamp)
    : FhirUpdateRequest(
        RequestSchema: "http",
        Tenant,
        RequestId,
        RequestPath: string.Empty,
        QueryString: null,
        Headers: new Dictionary<string, StringValues>(),
        ResourceName,
        Resource,
        ResourceId,
        TimeStamp);
        