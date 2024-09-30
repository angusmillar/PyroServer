using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirBundleService;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirBatchOrTransactionHandler(
    IValidator validator, 
    IFhirBundleServiceFactory fhirBundleServiceFactory) 
    : IRequestHandler<FhirBatchOrTransactionRequest, FhirResourceResponse>
{
    public async Task<FhirResourceResponse> Handle(FhirBatchOrTransactionRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult validatorResult = validator.Validate(request);
        if (!validatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(validatorResult);
        }
        
        if (request.Resource is not Bundle bundle)
        {
            throw new FhirFatalException(httpStatusCode: HttpStatusCode.ServiceUnavailable, "The FHIR Service Base URL can only accept resource types of Bundle");
        }

        if (bundle.Type is null)
        {
            throw new FhirErrorException(httpStatusCode: HttpStatusCode.BadRequest, $"The FHIR Service Base URL can only accept FHIR Bundles with a populated bundle.type");
        }

        IFhirBundleService fhirBundleService = fhirBundleServiceFactory.Resolve(GetBundleTypeBatchOrTransactionOrThrow(bundle.Type));
        
        return await fhirBundleService.Process(new FhirBundleRequest(
            RequestSchema: request.RequestSchema,
            Tenant: request.tenant,
            RequestPath: request.RequestPath,
            QueryString: request.QueryString,
            Headers: request.Headers,
            Bundle: bundle,
            TimeStamp: request.TimeStamp)
            , cancellationToken: cancellationToken);
    }

    private static FhirResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        return new FhirResourceResponse(
            Resource: validatorResult.GetOperationOutcome(), 
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>());
    }
    
    private BundleType GetBundleTypeBatchOrTransactionOrThrow(Bundle.BundleType? type)
    {
        switch (type)
        {
            case Bundle.BundleType.Transaction:
                return BundleType.Transaction;
            case Bundle.BundleType.Batch:
                return BundleType.Batch;
            default:
                throw new FhirErrorException(httpStatusCode: HttpStatusCode.BadRequest, 
                    message: $"The FHIR Service Base URL can only accept FHIR Bundles of type {Bundle.BundleType.Transaction.GetLiteral()} or {Bundle.BundleType.Batch.GetLiteral()}");
        }
    }
}