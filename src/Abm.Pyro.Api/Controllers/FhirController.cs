using Hl7.Fhir.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Abm.Pyro.Api.Extensions;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Api.Controllers;

[Route("fhir")]
[ApiController]
public class FhirController(
  IMediator mediator,
  IDateTimeProvider dateTimeProvider) : ControllerBase
{
  [HttpPost]
  public async Task<ActionResult<Resource>> Base([FromBody]Resource resource, CancellationToken cancellationToken)
  {
    var fhirResourceConditionalCreateRequest = new FhirBatchOrTransactionRequest(
      RequestSchema: Request.Scheme,
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
  
  [HttpPost("{resourceName}")]
  public async Task<ActionResult<Resource>> Post(string resourceName, [FromBody]Resource resource, CancellationToken cancellationToken)
  {
    var fhirResourceConditionalCreateRequest = new FhirConditionalCreateRequest(
      RequestSchema: Request.Scheme,
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
  public async Task<ActionResult<Resource>> Put(string resourceName, string resourceId, [FromBody]Resource resource, CancellationToken cancellationToken)
  {
    FhirUpdateRequest fhirResourceNameUpdateRequest = new FhirUpdateRequest(
      RequestSchema: Request.Scheme,
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
  public async Task<ActionResult<Resource>> ConditionalPut(string resourceName, [FromBody]Resource resource, CancellationToken cancellationToken)
  {
    FhirConditionalUpdateRequest fhirResourceNameUpdateRequest = new FhirConditionalUpdateRequest(
      RequestSchema: Request.Scheme,
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
  public async Task<ActionResult<Resource>> Delete(string resourceName, string resourceId, CancellationToken cancellationToken)
  {
    FhirDeleteRequest request = new FhirDeleteRequest(
      RequestSchema: Request.Scheme,
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
  public async Task<ActionResult<Resource>> ConditionalDelete(string resourceName, CancellationToken cancellationToken)
  {
    FhirConditionalDeleteRequest request = new FhirConditionalDeleteRequest(
      RequestSchema: Request.Scheme,
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
  public async Task<ActionResult<Resource>> Get(string resourceName, string resourceId, CancellationToken cancellationToken)
  {
    var fhirReadQuery = new FhirReadRequest(
      RequestSchema: Request.Scheme,
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
  public async Task<ActionResult<Resource>> GetHistorySystemLevel(CancellationToken cancellationToken)
  {
    var fhirHistorySystemLevelQuery = new FhirHistorySystemLevelRequest(
      RequestSchema: Request.Scheme,
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(), 
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirHistorySystemLevelQuery, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpGet("metadata")]
  public async Task<ActionResult<Resource>> GetMetadata(CancellationToken cancellationToken)
  {
    var fhirMetaDataRequest = new FhirMetaDataRequest(
      RequestSchema: Request.Scheme,
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(), 
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirMetaDataRequest, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpGet("{resourceName}/_history")]
  public async Task<ActionResult<Resource>> GetHistoryTypeLevel(string resourceName, CancellationToken cancellationToken)
  {
    var fhirHistoryResourceQuery = new FhirHistoryTypeLevelRequest(
      RequestSchema: Request.Scheme,
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName, 
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirHistoryResourceQuery, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpGet("{resourceName}/{resourceId}/_history")]
  public async Task<ActionResult<Resource>> GetHistoryInstanceLevel(string resourceName, string resourceId, CancellationToken cancellationToken)
  {
    var fhirHistoryResourceIdQuery = new FhirHistoryInstanceLevelRequest(
      RequestSchema: Request.Scheme,
      RequestPath: Request.Path,
      QueryString: Request.QueryString.Value,
      Headers: Request.Headers.GetDictionary(),
      ResourceName: resourceName,
      ResourceId: resourceId, 
      TimeStamp: dateTimeProvider.Now);

    FhirResourceResponse fhirResponse = await mediator.Send(fhirHistoryResourceIdQuery, cancellationToken);
    
    Response.Headers.AppendRange(fhirResponse.Headers);
    
    return  StatusCode((int)fhirResponse.HttpStatusCode, fhirResponse.Resource);
        
  }
  
  [HttpGet("{resourceName}/{resourceId}/_history/{historyId}")]
  public async Task<ActionResult<Resource>> GetHistoryInstanceLevel(string resourceName, string resourceId, string historyId, CancellationToken cancellationToken)
  {
    var fhirVersionReadRequest = new FhirVersionReadRequest(
      RequestSchema: Request.Scheme,
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
  public async Task<ActionResult<Resource>> Search(string resourceName, CancellationToken cancellationToken)
  {
    FhirSearchRequest fhirResourceNameSearchRequest = new FhirSearchRequest(
      RequestSchema: Request.Scheme,
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
