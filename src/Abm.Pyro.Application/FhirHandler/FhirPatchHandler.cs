using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirPatch;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Application.FhirValidateService;
using Abm.Pyro.Application.Indexing;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Dispatcher;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Indexing;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Notification;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using SummaryType = Hl7.Fhir.Rest.SummaryType;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirPatchHandler(
    ILogger<FhirPatchHandler> logger,
    IValidator validator,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IResourceStoreGetForUpdateByResourceId resourceStoreGetForUpdateByResourceId,
    IResourceStoreGetByResourceStoreId resourceStoreGetByResourceStoreId,
    IResourceStoreAdd resourceStoreAdd,
    IIndexer indexer,
    IFhirSerializationSupport fhirSerializationSupport,
    IFhirDeSerializationSupport fhirDeSerializationSupport,
    IResourceStoreUpdate resourceStoreUpdate,
    IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport,
    IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport,
    IOperationOutcomeSupport operationOutcomeSupport,
    IPreferredReturnTypeService preferredReturnTypeService,
    IOptions<IndexingSettings> indexingSettingsOptions,
    IRepositoryEventCollector repositoryEventCollector,
    IServiceSettingsCache serviceSettingsCache,
    IFhirValidateEngine fhirValidateEngine,
    IFhirPathPatchService fhirPathPatchService)
    : IRequestHandler<FhirPatchRequest, FhirOptionalResourceResponse>, IFhirPatchHandler
{
    private ResourceStoreUpdateProjection? _previousResourceStore;

    public async Task<FhirOptionalResourceResponse> Handle(
        string tenant,
        string requestId,
        string resourceId,
        string resourceName,
        Parameters patchParameters,
        Dictionary<string, StringValues> headers,
        CancellationToken cancellationToken,
        ResourceStoreUpdateProjection? previousResourceStore = null)
    {
        _previousResourceStore = previousResourceStore;

        return await Handle(new FhirPatchRequest(
            RequestSchema: "http",
            Tenant: tenant,
            RequestId: requestId,
            RequestPath: string.Empty,
            QueryString: null,
            Headers: headers,
            ResourceName: resourceName,
            Resource: patchParameters,
            ResourceId: resourceId,
            TimeStamp: DateTimeOffset.Now), cancellationToken);
    }

    public async Task<FhirOptionalResourceResponse> Handle(
        FhirPatchRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult validatorResult = validator.Validate(request);
        if (!validatorResult.IsValid)
            return InvalidValidatorResultResponse(validatorResult);

        // The validator guarantees Resource is Parameters, but guard defensively
        if (request.Resource is not Parameters patchParameters)
            return InvalidBodyTypeResponse(request.Resource.TypeName);

        FhirResourceTypeId fhirResourceType =
            fhirResourceTypeSupport.GetRequiredFhirResourceType(request.ResourceName);

        if (_previousResourceStore is null)
        {
            _previousResourceStore =
                await resourceStoreGetForUpdateByResourceId.Get(fhirResourceType, request.ResourceId);
        }

        // PATCH never creates — 404 when the resource does not exist
        if (_previousResourceStore is null || _previousResourceStore.IsDeleted)
        {
            return NotFoundResponse(request.ResourceId, request.ResourceName);
        }

        if (IfMatchPreconditionFailure(request.Headers, _previousResourceStore.VersionId,
                out FhirOptionalResourceResponse? preconditionFailureResponse))
        {
            return preconditionFailureResponse!;
        }

        // Load the full resource JSON so we can apply the patch
        ResourceStore? currentResourceStore =
            await resourceStoreGetByResourceStoreId.Get(_previousResourceStore.ResourceStoreId!.Value);

        if (currentResourceStore is null)
            throw new ApplicationException(
                $"ResourceStore row with id {_previousResourceStore.ResourceStoreId} could not be found after projection load.");

        Resource? currentResource = fhirDeSerializationSupport.ToResource(currentResourceStore.Json);
        if (currentResource is null)
            throw new ApplicationException(
                $"Failed to deserialise stored JSON for {request.ResourceName}/{request.ResourceId}.");

        Resource patchedResource;
        try
        {
            patchedResource = fhirPathPatchService.Apply(currentResource, patchParameters);
        }
        catch (FhirErrorException ex)
        {
            return PatchApplicationFailureResponse(ex);
        }

        // Ensure the logical id is preserved after the patch round-trip
        patchedResource.Id = request.ResourceId;

        if ((await serviceSettingsCache.GetFhirValidationSettings()).ValidateOnUpdate)
        {
            OperationOutcome operationOutcome = await fhirValidateEngine.Validate(patchedResource);
            if (!operationOutcome.Success)
                return InvalidFhirProfileValidationResultResponse(operationOutcome);
        }

        IndexerOutcome indexerOutcome = await indexer.Process(patchedResource, fhirResourceType);

        int updatedVersionId = _previousResourceStore.VersionId + 1;
        SetResourceMeta(patchedResource, updatedVersionId, request.TimeStamp);

        var updatedResourceStore = new ResourceStore(
            resourceStoreId: null,
            resourceId: patchedResource.Id,
            versionId: updatedVersionId,
            isCurrent: true,
            isDeleted: false,
            resourceType: fhirResourceType,
            httpVerb: request.HttpVerbId,
            json: fhirSerializationSupport.ToJson(patchedResource, SummaryType.False, pretty: false),
            lastUpdatedUtc: patchedResource.Meta!.LastUpdated!.Value.UtcDateTime,
            indexReferenceList: indexerOutcome.ReferenceIndexList,
            indexStringList: indexerOutcome.StringIndexList,
            indexDateTimeList: indexerOutcome.DateTimeIndexList,
            indexQuantityList: indexerOutcome.QuantityIndexList,
            indexTokenList: indexerOutcome.TokenIndexList,
            indexUriList: indexerOutcome.UriIndexList,
            rowVersion: 0
        );

        _previousResourceStore.IsCurrent = false;
        await resourceStoreUpdate.Update(_previousResourceStore,
            deleteFhirIndexes: indexingSettingsOptions.Value.RemoveHistoricResourceIndexesOnUpdateOrDelete);
        updatedResourceStore = await resourceStoreAdd.Add(updatedResourceStore);

        repositoryEventCollector.Add(
            resourceType: updatedResourceStore.ResourceType,
            repositoryEventType: RepositoryEventType.Update,
            resourceId: updatedResourceStore.ResourceId);

        var responseHeaders = fhirResponseHttpHeaderSupport.ForUpdate(
            lastUpdatedUtc: updatedResourceStore.LastUpdatedUtc,
            versionId: updatedResourceStore.VersionId,
            requestTimeStamp: request.TimeStamp);

        logger.LogDebug("PATCH applied to {ResourceName}/{ResourceId}, new version {VersionId}",
            request.ResourceName, request.ResourceId, updatedVersionId);

        return preferredReturnTypeService.GetResponse(
            httpStatusCode: HttpStatusCode.OK,
            resource: patchedResource,
            versionId: updatedResourceStore.VersionId,
            requestHeaders: request.Headers,
            responseHeaders: responseHeaders,
            repositoryEventQueue: repositoryEventCollector);
    }

    private static void SetResourceMeta(Resource resource, int versionId, DateTimeOffset requestTimeStamp)
    {
        resource.Meta ??= new Meta();
        resource.Meta.VersionId = versionId.ToString();
        resource.Meta.LastUpdated = requestTimeStamp;
    }

    private bool IfMatchPreconditionFailure(
        Dictionary<string, StringValues> requestHeaders,
        int resourceStoreVersionId,
        out FhirOptionalResourceResponse? fhirResourceResponse)
    {
        repositoryEventCollector.Clear();
        fhirResourceResponse = null;
        int? ifMatchVersion = fhirRequestHttpHeaderSupport.GetIfMatch(requestHeaders);
        if (ifMatchVersion is null || resourceStoreVersionId == ifMatchVersion.Value)
            return false;

        fhirResourceResponse = new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(
            [
                $"Optimistic concurrency via the request's {HttpHeaderName.IfMatch} header has caused a precondition " +
                $"failure. The If-Match version was {ifMatchVersion} but the server holds version {resourceStoreVersionId}."
            ]),
            HttpStatusCode: HttpStatusCode.PreconditionFailed,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
        return true;
    }

    private FhirOptionalResourceResponse NotFoundResponse(string resourceId, string resourceName)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(
            [
                $"Resource {resourceName}/{resourceId} was not found. " +
                "PATCH does not create resources — use POST or PUT."
            ]),
            HttpStatusCode: HttpStatusCode.NotFound,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }

    private FhirOptionalResourceResponse PatchApplicationFailureResponse(FhirErrorException ex)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: ex.OperationOutcome
                      ?? operationOutcomeSupport.GetError(ex.MessageList ?? [ex.Message]),
            HttpStatusCode: ex.HttpStatusCode,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }

    private FhirOptionalResourceResponse InvalidFhirProfileValidationResultResponse(OperationOutcome operationOutcome)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: operationOutcome,
            HttpStatusCode: HttpStatusCode.UnprocessableEntity,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
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

    private FhirOptionalResourceResponse InvalidBodyTypeResponse(string actualTypeName)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: operationOutcomeSupport.GetError(
            [
                $"The body of a PATCH request must be a FHIR Parameters resource, but received '{actualTypeName}'."
            ]),
            HttpStatusCode: HttpStatusCode.BadRequest,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
}
