using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.FhirRequest;

public abstract record FhirResourceNameResourceRequestBase(
    string RequestSchema,
    string Tenant,
    string RequestId,
    string RequestPath,
    string? QueryString,
    Dictionary<string, StringValues> Headers, 
    string ResourceName, 
    Resource Resource,
    HttpVerbId HttpVerbId,
    DateTimeOffset TimeStamp) :
    FhirResourceRequestBase(
        RequestSchema,
        Tenant,
        RequestId,
        RequestPath,
        QueryString,
        Headers, 
        Resource,
        HttpVerbId,
        TimeStamp);

public abstract record FhirResourceRequestBase(
    string RequestSchema,
    string Tenant,
    string RequestId,
    string RequestPath,
    string? QueryString,
    Dictionary<string, StringValues> Headers, 
    Resource Resource,
    HttpVerbId HttpVerbId,
    DateTimeOffset TimeStamp) :
    FhirRequestBase(
        RequestSchema,
        Tenant,
        RequestId,
        RequestPath,
        QueryString,
        Headers,
        HttpVerbId,
        TimeStamp);

public abstract record FhirResourceNameResourceNullableResourceIdRequestBase(
    string RequestSchema,
    string Tenant,
    string RequestId,
    string RequestPath,
    string? QueryString,
    Dictionary<string, StringValues> Headers, 
    string ResourceName, 
    string? ResourceId, 
    Resource Resource,
    HttpVerbId HttpVerbId,
    DateTimeOffset TimeStamp) :
    FhirResourceNameNullableResourceIdRequestBase(
        RequestSchema,
        Tenant,
        RequestId,
        RequestPath,
        QueryString,
        Headers, 
        ResourceName, 
        ResourceId,
        HttpVerbId,
        TimeStamp);

public abstract record FhirResourceNameNullableResourceIdRequestBase(
    string RequestSchema,
    string Tenant,
    string RequestId,
    string RequestPath,
    string? QueryString,
    Dictionary<string, StringValues> Headers, 
    string ResourceName, 
    string? ResourceId,
    HttpVerbId HttpVerbId,
    DateTimeOffset TimeStamp) :
    FhirResourceNameRequestBase(
        RequestSchema,
        Tenant,
        RequestId,
        RequestPath,
        QueryString,
        Headers, 
        ResourceName,
        HttpVerbId,
        TimeStamp);

public abstract record FhirResourceNameResourceIdRequestBase(
    string RequestSchema,
    string Tenant,
    string RequestId,
    string RequestPath,
    string? QueryString,
    Dictionary<string, StringValues> Headers, 
    string ResourceName, 
    string ResourceId,
    HttpVerbId HttpVerbId,
    DateTimeOffset TimeStamp) :
    FhirResourceNameRequestBase(
        RequestSchema,
        Tenant,
        RequestId,
        RequestPath,
        QueryString,
        Headers, 
        ResourceName,
        HttpVerbId,
        TimeStamp);

public abstract record FhirResourceNameRequestBase(
    string RequestSchema, 
    string Tenant,
    string RequestId,
    string RequestPath,
    string? QueryString,
    Dictionary<string, StringValues> Headers, 
    string ResourceName,
    HttpVerbId HttpVerbId,
    DateTimeOffset TimeStamp) :
    FhirRequestBase(
        RequestSchema,
        Tenant,
        RequestId,
        RequestPath,
        QueryString,
        Headers,
        HttpVerbId,
        TimeStamp);

public abstract record FhirRequestBase(
    string RequestSchema,
    string Tenant,
    string RequestId,
    string RequestPath,
    string? QueryString, 
    Dictionary<string, StringValues> Headers,
    HttpVerbId HttpVerbId,
    DateTimeOffset TimeStamp);
