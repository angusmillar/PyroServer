using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirOperation;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Notification;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Validation;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirSystemLevelOperationHandler(
    IValidator validator,
    IFhirOperationFactory fhirOperationFactory,
    IRepositoryEventCollector repositoryEventCollector,
    IOperationOutcomeSupport operationOutcomeSupport)
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
        
        IFhirOperationService? fhirOperationService = fhirOperationFactory.Get(fhirOperationLevel: FhirOperationLevel.System, request.OperationName);
        if (fhirOperationService is null)
        {
            return InvalidFhirOperationNameResultResponse(fhirOperationName: request.OperationName);
        }

        if (fhirOperationService is not IFhirSystemOperationService fhirSystemOperationService)
        {
            throw new InvalidCastException(nameof(fhirOperationService));
        }


        await Task.Delay(5000, cancellationToken);
        fhirSystemOperationService.Handle(request: request.OperationName);
        
        
        
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
    
    private FhirResourceResponse InvalidFhirOperationNameResultResponse(string fhirOperationName)
    {
        repositoryEventCollector.Clear();
        return new FhirResourceResponse(
            Resource: operationOutcomeSupport.GetError(messageList: 
                [$"The FHIR operation named: {fhirOperationName} is not supported by this server at the systems level."]), 
            HttpStatusCode: HttpStatusCode.BadRequest,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
    
}