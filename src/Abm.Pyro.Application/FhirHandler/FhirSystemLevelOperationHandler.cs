using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirOperation;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Notification;
using MediatR;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirSystemLevelOperationHandler(
    IValidator validator,
    IFhirOperationFactory fhirOperationFactory,
    IRepositoryEventCollector repositoryEventCollector,
    IOperationOutcomeSupport operationOutcomeSupport)
    : FhirOperationBaseHandler(repositoryEventCollector: repositoryEventCollector, operationOutcomeSupport: operationOutcomeSupport), 
        IRequestHandler<FhirSystemLevelOperationRequest, FhirResourceResponse>
{
    private const FhirOperationLevel OperationSystemLevel = FhirOperationLevel.System;
    
    public async Task<FhirResourceResponse> Handle(FhirSystemLevelOperationRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult requestValidatorResult = validator.Validate(request);
        if (!requestValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(requestValidatorResult);
        }
        
        IFhirOperationService? fhirOperationService = fhirOperationFactory.Get(fhirOperationLevel: OperationSystemLevel, request.OperationName);
        if (fhirOperationService is null)
        {
            return InvalidFhirOperationNameResultResponse(fhirOperationLevel: OperationSystemLevel, fhirOperationName: request.OperationName);
        }

        if (fhirOperationService is not IFhirSystemOperationService fhirSystemOperationService)
        {
            throw new InvalidCastException(nameof(fhirOperationService));
        }

        return await fhirSystemOperationService.Handle(request: request);
        
    }
    
}