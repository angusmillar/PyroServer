using Abm.Pyro.Application.FhirRequest;
using MediatR;
using Abm.Pyro.Domain.FhirSupport;

namespace Abm.Pyro.Application.Behavior;

public class CorrelationBehavior<TRequest, TResponse>(
  IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport,
  IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport)
  : IPipelineBehavior<TRequest, TResponse>
  where TRequest : notnull
{
  public async Task<TResponse> Handle(
    TRequest request, 
    RequestHandlerDelegate<TResponse> next, 
    CancellationToken cancellationToken)
  {
    string xRequestId = GuidSupport.NewFhirGuid();
    string? xCorrelationId = null;
    if (request is FhirRequestBase fhirRequestBase)
    {
      xCorrelationId = fhirRequestHttpHeaderSupport.GetXRequestId(fhirRequestBase.Headers);
    }
    
    var response = await next();
    
    if (response is FhirResponse.FhirResponse fhirResponse)
    {
      fhirResponseHttpHeaderSupport.AddXRequestId(fhirResponse.Headers, xRequestId);
      if (xCorrelationId is not null)
      {
        fhirResponseHttpHeaderSupport.AddXCorrelationId(fhirResponse.Headers, xCorrelationId);  
      }
    }

    return response;
  }
}
