using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Support;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirSupport;

public class FhirResponseHttpHeaderSupport : IFhirResponseHttpHeaderSupport
{
  public void AddXRequestId(Dictionary<string, StringValues> responseHeaders , string xRequestId)
  {
    responseHeaders[HttpHeaderName.XRequestId] = xRequestId;
  }

  public void AddXCorrelationId(Dictionary<string, StringValues> responseHeaders , string xCorrelationId)
  {
    responseHeaders[HttpHeaderName.XCorrelationId] = xCorrelationId;
  }
  
  public Dictionary<string, StringValues> ForCreate(
    FhirResourceTypeId resourceType, 
    DateTime lastUpdatedUtc, 
    string resourceId, 
    int versionId, 
    DateTimeOffset requestTimeStamp, 
    string requestSchema, 
    string serviceBaseUrl)
  {
    var headers = new Dictionary<string, StringValues>();
    AddDate(headers, requestTimeStamp);
    AddLastModified(headers, lastUpdatedUtc);
    AddETag(headers, versionId);
    AddLocation(headers, requestSchema, serviceBaseUrl, resourceType, resourceId, versionId);
    return headers;
  }

  public Dictionary<string, StringValues> ForUpdate(FhirResourceTypeId resourceType, DateTime lastUpdatedUtc, string resourceId, int versionId, DateTimeOffset requestTimeStamp)
  {
    var headers = new Dictionary<string, StringValues>();
    AddDate(headers, requestTimeStamp);
    AddLastModified(headers, lastUpdatedUtc);
    AddETag(headers, versionId);
    return headers;
  }

  public Dictionary<string, StringValues> ForDelete(DateTimeOffset requestTimeStamp, int? versionId = null)
  {
    var headers = new Dictionary<string, StringValues>();
    AddDate(headers, requestTimeStamp);
    if (versionId.HasValue)
    {
      AddETag(headers, versionId.Value);
    }
    return headers;
  }

  public Dictionary<string, StringValues> ForRead(DateTime lastUpdatedUtc, int versionId, DateTimeOffset requestTimeStamp)
  {
    var headers = new Dictionary<string, StringValues>();
    AddDate(headers, requestTimeStamp);
    AddLastModified(headers, lastUpdatedUtc);
    AddETag(headers, versionId);
    return headers;
  }

  public Dictionary<string, StringValues> ForSearch(DateTimeOffset requestTimeStamp)
  {
    var headers = new Dictionary<string, StringValues>();
    AddDate(headers, requestTimeStamp);
    return headers;
  }

  private void AddLastModified(Dictionary<string, StringValues> headers, DateTime lastUpdatedUtc)
  {
    headers.Add(HttpHeaderName.LastModified, new StringValues(lastUpdatedUtc.ToString("r", System.Globalization.CultureInfo.CurrentCulture)));
  }

  private void AddETag(Dictionary<string, StringValues> headers, int versionId)
  {
    headers.Add(HttpHeaderName.ETag, new StringValues(StringSupport.GetEtag(versionId)));
  }

  private void AddLocation(Dictionary<string, StringValues> headers, string requestSchema, string serviceBaseUrl, FhirResourceTypeId resourceType, string resourceId, int versionId)
  {
    headers.Add(HttpHeaderName.Location, $"{requestSchema}://{serviceBaseUrl}/{resourceType.ToString()}/{resourceId}/_history/{versionId.ToString()}");
  }
  
  private void AddDate(Dictionary<string, StringValues> headers, DateTimeOffset requestTimeStamp)
  {
    headers.Add(HttpHeaderName.Date, requestTimeStamp.ToUniversalTime().ToString("r"));
  }

}
