using Hl7.Fhir.Model;
using FhirUri = Abm.Pyro.Domain.FhirSupport.FhirUri;

namespace Abm.Pyro.Application.FhirBundleService;

public class BundleEntryTransactionMetaData(FhirUri forFullUrl, FhirUri requestUrl)
{
    public FhirUri ForFullUrl { get; init; } = forFullUrl;
    public FhirUri RequestUrl { get; init; } = requestUrl;
    public bool IsSuccess => FailureOperationOutcome is null;
    public bool IsFailure => !IsSuccess;
    public ResourceUpdateInfo? ResourceUpdateInfo { get; set; }
    public OperationOutcome? FailureOperationOutcome { get; set; }
    
}