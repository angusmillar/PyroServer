using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.Notification;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirSystemLevelOperationHandler(
    IValidator validator,
    IRepositoryEventCollector repositoryEventCollector)
    : IRequestHandler<FhirSystemLevelOperationRequest, FhirResourceResponse>
{
    public async Task<FhirResourceResponse> Handle(FhirSystemLevelOperationRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult requestValidatorResult = validator.Validate(request);
        if (!requestValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(requestValidatorResult);
        }
        
        //ToDo: Here we need a new ServiceLocator /Factor to get the correct FHIR $Operation Service
        
        return new FhirResourceResponse(
            Resource: new Patient(), //ToDo Only a dummy set 
            HttpStatusCode: HttpStatusCode.OK,
            Headers: new Dictionary<string, StringValues>(), 
            ResourceOutcomeInfo: null,
            RepositoryEventCollector: repositoryEventCollector);
    }
    
    private FhirResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        repositoryEventCollector.Clear();
        return new FhirResourceResponse(
            Resource: validatorResult.GetOperationOutcome(), 
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
    
}