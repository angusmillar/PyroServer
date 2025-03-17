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

public class FhirTypeLevelOperationHandler(
    IValidator validator,
    IFhirOperationFactory fhirOperationFactory,
    IRepositoryEventCollector repositoryEventCollector,
    IOperationOutcomeSupport operationOutcomeSupport)
    : FhirOperationBaseHandler(
            repositoryEventCollector: repositoryEventCollector,
            operationOutcomeSupport: operationOutcomeSupport),
        IRequestHandler<FhirTypeLevelOperationRequest, FhirResourceResponse>
{
    private const FhirOperationLevel OperationTypeLevel = FhirOperationLevel.Type;

    public async Task<FhirResourceResponse> Handle(
        FhirTypeLevelOperationRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult requestValidatorResult = validator.Validate(request);
        if (!requestValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(requestValidatorResult);
        }

        IFhirOperationService? fhirOperationService = fhirOperationFactory.Get(
            fhirOperationLevel: OperationTypeLevel,
            request.OperationName);

        if (fhirOperationService is null)
        {
            return InvalidFhirOperationNameResultResponse(
                fhirOperationLevel: OperationTypeLevel,
                fhirOperationName: request.OperationName);
        }

        if (fhirOperationService is not IFhirTypeOperationService fhirTypeOperationService)
        {
            throw new InvalidCastException(nameof(fhirOperationService));
        }
        
        await Task.Delay(0, cancellationToken);
        
        return await fhirTypeOperationService.Handle(request: request);
      
    }
}