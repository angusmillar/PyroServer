using Abm.Pyro.Domain.Enums;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirResponseHttpHeaderSupport
{
    void AddXRequestId(Dictionary<string, StringValues> responseHeaders, string xRequestId);
    void AddXCorrelationId(Dictionary<string, StringValues> responseHeaders, string xCorrelationId);
    Dictionary<string, StringValues> ForCreate(FhirResourceTypeId resourceType, DateTime lastUpdatedUtc, string resourceId, int versionId, DateTimeOffset requestTimeStamp);
    Dictionary<string, StringValues> ForUpdate(FhirResourceTypeId resourceType, DateTime lastUpdatedUtc, string resourceId, int versionId, DateTimeOffset requestTimeStamp);
    Dictionary<string, StringValues> ForDelete(DateTimeOffset requestTimeStamp, int? versionId = null);
    Dictionary<string, StringValues> ForRead(DateTime lastUpdatedUtc, int versionId, DateTimeOffset requestTimeStamp);
    Dictionary<string, StringValues> ForSearch(DateTimeOffset requestTimeStamp);
}