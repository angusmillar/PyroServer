using System.Diagnostics;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Application.Behavior;

public class LoggingBehavior<TRequest, TResponse>(
  ILogger<LoggingBehavior<TRequest, TResponse>> logger,
  IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport)
  : IPipelineBehavior<TRequest, TResponse>
  where TRequest : notnull
{
  public async Task<TResponse> Handle(
    TRequest request, 
    RequestHandlerDelegate<TResponse> next, 
    CancellationToken cancellationToken)
  {
    Stopwatch stopWatch = new Stopwatch();
    stopWatch.Start();
    string requestId = RequestDebugLogging(request);
    
    var response = await next();

    ResponseLogging(response, requestId);
    stopWatch.Stop();
    InfoLogTimeElapsed(stopWatch);
    return response;
  }

  private void InfoLogTimeElapsed(Stopwatch stopWatch)
  {
    logger.LogInformation("Execution Time: {ElapsedTime} ", ToHumanReadableTimeSpan(stopWatch.Elapsed));
  }

  private string RequestDebugLogging(TRequest request)
  {
    string requestId = String.Empty;
    if (logger.IsEnabled(LogLevel.Debug))
    {
      if (request is FhirRequestBase fhirRequestBase)
      {
        requestId = fhirRequestBase.RequestId;
        logger.LogDebug("---------- Request ------------------------------------------------------------");
        DebugLogBaseRequestUrl(fhirRequestBase);
        DebugLogRequestId(fhirRequestBase.RequestId);
        DebugLogTenant(fhirRequestBase.Tenant);
        DebugLogHeaders(fhirRequestBase.Headers);
      }
    }
    return requestId;
  }

  private void ResponseLogging(TResponse response, string requestId)
  {
    if (logger.IsEnabled(LogLevel.Debug))
    {
      if (response is FhirResponse.FhirResponse fhirResponse)
      {
        logger.LogDebug("---------- Response -----------------------------------------------------------");
        logger.LogDebug("  Status: {HttpStatus} ({HttpStatusCode})", (int)fhirResponse.HttpStatusCode, fhirResponse.HttpStatusCode.ToString());
        DebugLogRequestId(requestId);
        DebugLogHeaders(fhirResponse.Headers);
      }
      if (response is FhirResourceResponse fhirResourceResponse)
      {
        DebugLogBodyFhirResourceName(fhirResourceResponse.Resource.TypeName);
      }
      if (response is FhirOptionalResourceResponse fhirOptionalResourceResponse)
      {
        string bodyResourceType = "[None]";
        if (fhirOptionalResourceResponse.Resource is not null)
        {
          bodyResourceType = fhirOptionalResourceResponse.Resource.TypeName;
        }
        DebugLogBodyFhirResourceName(bodyResourceType);
      }
    }
  }

  private void DebugLogBaseRequestUrl(FhirRequestBase fhirRequestBase)
  {
    logger.LogDebug("  {Verb} [Base]{RequestUri}", fhirRequestBase.HttpVerbId.GetCode(), $"{fhirRequestBase.RequestPath}{fhirRequestBase.QueryString.ToQueryHumanReadableQueryString()}");
  }

  private void DebugLogTenant(string tenantDisplay)
  {
    logger.LogDebug("  Tenant: {TenantDisplay}", tenantDisplay);
  }

  private void DebugLogRequestId(string requestId)
  {
    logger.LogDebug("  Request Id: {RequestId}", requestId);
  }

  private void DebugLogBodyFhirResourceName(string body)
  {
    logger.LogDebug("  Body: FHIR resource type {ResourceType} ", body);
  }

  private void DebugLogHeaders(Dictionary<string, StringValues> headers)
  {
    logger.LogDebug("  Headers:");
    foreach (var header in fhirRequestHttpHeaderSupport.AllHeadersHumanDisplay(headers))
    {
      logger.LogDebug("    {HeaderName}:{HeaderValue}", header.name.PadRight(30, ' '), header.value.PadRight(20, ' '));
    }
  }

  private static string ToHumanReadableTimeSpan (TimeSpan t)
  {
    if (t.TotalSeconds <= 1) {
      return $@"{t:fff} ms";
    }
    if (t.TotalMinutes <= 1) {
      return $@"{t:%s}.{t:ff} s";
    }
    if (t.TotalHours <= 1) {
      return $@"{t:%m} m {t:%s}.{t:ff} s";
    }
    if (t.TotalDays <= 1) {
      return $@"{t:%h} h {t:%m} m {t:%s}.{t:ff} s";
    }

    return $@"{t:%d} d {t:%h} h {t:%m} m {t:%s}.{t:ff} s";
  }
  
}
