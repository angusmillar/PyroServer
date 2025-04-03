using System.Net;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Notification;
using Abm.Pyro.Domain.Validation;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirHandler;

public abstract class FhirOperationBaseHandler(
    IRepositoryEventCollector repositoryEventCollector, 
    IOperationOutcomeSupport operationOutcomeSupport)
{
    protected virtual FhirResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        repositoryEventCollector.Clear();
        return new FhirResourceResponse(
            Resource: validatorResult.GetOperationOutcome(), 
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
    
    protected virtual FhirResourceResponse InvalidFhirOperationNameResultResponse(FhirOperationLevel fhirOperationLevel, string fhirOperationName)
    {
        repositoryEventCollector.Clear();
        return new FhirResourceResponse(
            Resource: operationOutcomeSupport.GetError(messageList: 
                [$"The {fhirOperationLevel} level FHIR operation named: ${fhirOperationName} is not supported by this server."]), 
            HttpStatusCode: HttpStatusCode.BadRequest,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
}