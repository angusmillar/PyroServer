using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Dispatcher;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Notification;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirConditionalPatchHandler(
    IValidator validator,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    ISearchQueryService searchQueryService,
    IResourceStoreSearch resourceStoreSearch,
    IFhirPatchHandler fhirPatchHandler,
    IOperationOutcomeSupport operationOutcomeSupport,
    IRepositoryEventCollector repositoryEventCollector)
    : IRequestHandler<FhirConditionalPatchRequest, FhirOptionalResourceResponse>, IFhirConditionalPatchHandler
{
    public async Task<FhirOptionalResourceResponse> Handle(
        string tenant,
        string requestId,
        string resourceName,
        string query,
        Parameters patchParameters,
        Dictionary<string, StringValues> headers,
        CancellationToken cancellationToken)
    {
        return await Handle(new FhirConditionalPatchRequest(
                RequestSchema: "https",
                Tenant: tenant,
                RequestId: requestId,
                RequestPath: string.Empty,
                QueryString: query,
                Headers: headers,
                ResourceName: resourceName,
                Resource: patchParameters,
                TimeStamp: DateTimeOffset.Now),
            cancellationToken: cancellationToken);
    }

    public async Task<FhirOptionalResourceResponse> Handle(
        FhirConditionalPatchRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult requestValidatorResult = validator.Validate(request);
        if (!requestValidatorResult.IsValid)
            return InvalidValidatorResultResponse(requestValidatorResult);

        if (request.Resource is not Parameters patchParameters)
            return InvalidBodyTypeResponse(request.Resource.TypeName);

        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.ResourceName);

        SearchQueryServiceOutcome searchQueryServiceOutcome =
            await searchQueryService.Process(fhirResourceType, request.QueryString);

        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            SearchQueryServiceOutcome: searchQueryServiceOutcome,
            Headers: request.Headers));
        if (!searchQueryValidatorResult.IsValid)
            return InvalidValidatorResultResponse(searchQueryValidatorResult);

        ResourceStoreSearchOutcome resourceStoreSearchOutcome =
            await resourceStoreSearch.GetSearch(searchQueryServiceOutcome);

        if (resourceStoreSearchOutcome.SearchTotal > 1)
            return PreconditionFailed();

        if (resourceStoreSearchOutcome.SearchTotal == 0)
            return NotFoundResponse(request.ResourceName);

        // Exactly one match — delegate to the standard patch handler
        string resourceId = resourceStoreSearchOutcome.ResourceStoreList.First().ResourceId;

        return await fhirPatchHandler.Handle(
            tenant: request.Tenant,
            requestId: request.RequestId,
            resourceId: resourceId,
            resourceName: request.ResourceName,
            patchParameters: patchParameters,
            headers: request.Headers,
            cancellationToken: cancellationToken);
    }

    private FhirOptionalResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: validatorResult.GetOperationOutcome(),
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }

    private FhirOptionalResourceResponse NotFoundResponse(string resourceName)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(
            [
                $"Conditional PATCH found no {resourceName} resources matching the supplied search criteria. " +
                "PATCH does not create resources — use PUT to upsert."
            ]),
            HttpStatusCode: HttpStatusCode.NotFound,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }

    private FhirOptionalResourceResponse PreconditionFailed()
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(
            [
                "Conditional PATCH criteria were not selective enough — more than one resource matched."
            ]),
            HttpStatusCode: HttpStatusCode.PreconditionFailed,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }

    private FhirOptionalResourceResponse InvalidBodyTypeResponse(string actualTypeName)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(
            [
                $"The body of a conditional PATCH request must be a FHIR Parameters resource, but received '{actualTypeName}'."
            ]),
            HttpStatusCode: HttpStatusCode.BadRequest,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
}
