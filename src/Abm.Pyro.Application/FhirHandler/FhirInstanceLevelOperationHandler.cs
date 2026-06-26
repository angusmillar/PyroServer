using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirOperation;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Notification;
using Abm.Pyro.Domain.Dispatcher;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirInstanceLevelOperationHandler(
    IValidator validator,
    IFhirOperationFactory fhirOperationFactory,
    IRepositoryEventCollector repositoryEventCollector,
    IOperationOutcomeSupport operationOutcomeSupport)
    : FhirOperationBaseHandler(
            repositoryEventCollector: repositoryEventCollector,
            operationOutcomeSupport: operationOutcomeSupport),
        IRequestHandler<FhirInstanceLevelOperationRequest, FhirResourceResponse>
{
    private const FhirOperationLevel OperationInstanceLevel = FhirOperationLevel.Instance;

    public async Task<FhirResourceResponse> Handle(
        FhirInstanceLevelOperationRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult requestValidatorResult = validator.Validate(request);
        if (!requestValidatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(requestValidatorResult);
        }

        IFhirOperationService? fhirOperationService = fhirOperationFactory.Get(
            fhirOperationLevel: OperationInstanceLevel,
            request.OperationName);

        if (fhirOperationService is null)
        {
            return InvalidFhirOperationNameResultResponse(
                fhirOperationLevel: OperationInstanceLevel,
                fhirOperationName: request.OperationName);
        }

        if (fhirOperationService is not IFhirInstanceOperationService fhirInstanceOperationService)
        {
            throw new InvalidCastException(nameof(fhirOperationService));
        }

        await Task.Delay(0, cancellationToken);
        
        return await fhirInstanceOperationService.Handle(request: request);
    }
}