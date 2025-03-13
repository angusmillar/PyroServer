using Hl7.Fhir.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Abm.Pyro.Api.Extensions;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Api.Controllers;

[Route("{tenant}")]
[ApiController]
public class FhirController(
  IMediator mediator,
  IDateTimeProvider dateTimeProvider) : ControllerBase
{
  
  [HttpPost]
  public async Task<ActionResult<Resource>> Base(string tenant, [FromBody]Resource resource, CancellationToken cancellationToken)
  {
    var fhirResourceConditionalCreateRequest = new FhirBatchOrTransactionRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      Resource: resource, 
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirResourceConditionalCreateRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    resource.AddAnnotation(Hl7.Fhir.Rest.SummaryType.False); 
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpPost("${operationName}")]
  public async Task<ActionResult<Resource>> Base(string tenant, string operationName, [FromBody]Resource resource, CancellationToken cancellationToken)
  {
    var fhirSystemLevelOperationRequest = new FhirSystemLevelOperationRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      OperationName: operationName,
      Resource: resource,
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirSystemLevelOperationRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    resource.AddAnnotation(Hl7.Fhir.Rest.SummaryType.False); 
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpPost("{resourceName}/${operationName}")]
  public async Task<ActionResult<Resource>> Base(string tenant, string resourceName, string operationName, [FromBody]Resource resource, CancellationToken cancellationToken)
  {
    var fhirSystemLevelOperationRequest = new FhirTypeLevelOperationRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      OperationName: operationName,
      ResourceName: resourceName,
      Resource: resource,
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirSystemLevelOperationRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    resource.AddAnnotation(Hl7.Fhir.Rest.SummaryType.False); 
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpPost("{resourceName}/{resourceId}/${operationName}")]
  public async Task<ActionResult<Resource>> Base(string tenant, string resourceName, string resourceId, string operationName, [FromBody]Resource resource, CancellationToken cancellationToken)
  {
    var fhirSystemLevelOperationRequest = new FhirInstanceLevelOperationRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: resourceId,
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      OperationName: operationName,
      ResourceName: resourceName,
      Resource: resource,
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirSystemLevelOperationRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    resource.AddAnnotation(Hl7.Fhir.Rest.SummaryType.False); 
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  
  [HttpPost("{resourceName}")]
  public async Task<ActionResult<Resource>> Post(string tenant, string resourceName, [FromBody]Resource resource, CancellationToken cancellationToken)
  {
    var fhirResourceConditionalCreateRequest = new FhirConditionalCreateRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName,
      Resource: resource, 
      TimeStamp: dateTimeProvider.Now);

    FhirOptionalResourceResponse fhirResponse = await mediator.Send(fhirResourceConditionalCreateRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    resource.AddAnnotation(Hl7.Fhir.Rest.SummaryType.False); 
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
  }
  
  [HttpPut("{resourceName}/{resourceId}")]
  public async Task<ActionResult<Resource>> Put(string tenant, string resourceName, string resourceId, [FromBody]Resource resource, CancellationToken cancellationToken)
  {
    FhirUpdateRequest fhirResourceNameUpdateRequest = new FhirUpdateRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName,
      ResourceId: resourceId,
      Resource: resource, 
      TimeStamp: dateTimeProvider.Now);

    FhirOptionalResourceResponse fhirResponse = await mediator.Send(fhirResourceNameUpdateRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    resource.AddAnnotation(Hl7.Fhir.Rest.SummaryType.False);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpPut("{resourceName}")]
  public async Task<ActionResult<Resource>> ConditionalPut(string tenant, string resourceName, [FromBody]Resource resource, CancellationToken cancellationToken)
  {
    FhirConditionalUpdateRequest fhirResourceNameUpdateRequest = new FhirConditionalUpdateRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName,
      Resource: resource, 
      TimeStamp: dateTimeProvider.Now);

    FhirOptionalResourceResponse fhirResponse = await mediator.Send(fhirResourceNameUpdateRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    resource.AddAnnotation(Hl7.Fhir.Rest.SummaryType.False);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpDelete("{resourceName}/{resourceId}")]
  public async Task<ActionResult<Resource>> Delete(string tenant, string resourceName, string resourceId, CancellationToken cancellationToken)
  {
    FhirDeleteRequest request = new FhirDeleteRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName,
      ResourceId: resourceId, 
      TimeStamp: dateTimeProvider.Now);

    FhirResponse fhirResponse = await mediator.Send(request, cancellationToken);
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode);
        
  }
  
  [HttpDelete("{resourceName}")]
  public async Task<ActionResult<Resource>> ConditionalDelete(string tenant, string resourceName, CancellationToken cancellationToken)
  {
    FhirConditionalDeleteRequest request = new FhirConditionalDeleteRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName, 
      TimeStamp: dateTimeProvider.Now);

    FhirOptionalResourceResponse fhirResponse = await mediator.Send(request, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpGet("{resourceName}/{resourceId}")]
  public async Task<ActionResult<Resource>> Get(string tenant, string resourceName, string resourceId, CancellationToken cancellationToken)
  {
    var fhirReadQuery = new FhirReadRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName,
      ResourceId: resourceId, 
      TimeStamp: dateTimeProvider.Now);

    FhirOptionalResourceResponse fhirResponse = await mediator.Send(fhirReadQuery, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpGet("_history")]
  public async Task<ActionResult<Resource>> GetHistorySystemLevel(string tenant, CancellationToken cancellationToken)
  {
    var fhirSystemLevelHistoryQuery = new FhirSystemLevelHistoryRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(), 
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirSystemLevelHistoryQuery, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpGet("metadata")]
  public async Task<ActionResult<Resource>> GetMetadata(string tenant, CancellationToken cancellationToken)
  {
    var fhirMetaDataRequest = new FhirMetaDataRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(), 
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirMetaDataRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpGet("{resourceName}/_history")]
  public async Task<ActionResult<Resource>> GetHistoryTypeLevel(string tenant, string resourceName, CancellationToken cancellationToken)
  {
    var fhirTypeLevelHistoryRequest = new FhirTypeLevelHistoryRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName, 
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirTypeLevelHistoryRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpGet("{resourceName}/{resourceId}/_history")]
  public async Task<ActionResult<Resource>> GetHistoryInstanceLevel(string tenant, string resourceName, string resourceId, CancellationToken cancellationToken)
  {
    var fhirInstanceLevelHistoryRequest = new FhirInstanceLevelHistoryRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName,
      ResourceId: resourceId, 
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirInstanceLevelHistoryRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpGet("{resourceName}/{resourceId}/_history/{historyId}")]
  public async Task<ActionResult<Resource>> GetHistoryInstance(string tenant, string resourceName, string resourceId, string historyId, CancellationToken cancellationToken)
  {
    var fhirVersionReadRequest = new FhirVersionReadRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName,
      ResourceId: resourceId, 
      HistoryId: historyId,
      TimeStamp: dateTimeProvider.Now);

    FhirOptionalResourceResponse fhirResponse = await mediator.Send(fhirVersionReadRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpGet("{resourceName}")] 
  public async Task<ActionResult<Resource>> Search(string tenant, string resourceName, CancellationToken cancellationToken)
  {
    FhirSearchRequest fhirResourceNameSearchRequest = new FhirSearchRequest(
      RequestSchema: Request.Scheme,
      Tenant: tenant,
      RequestId: GuidSupport.NewFhirGuid(),
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName, 
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirResourceNameSearchRequest, cancellationToken);
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
}
