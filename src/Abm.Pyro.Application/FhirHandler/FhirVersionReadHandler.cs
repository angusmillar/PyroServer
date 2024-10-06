using System.Globalization;
using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.Notification;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirVersionReadHandler(
    IValidator validator,
    IResourceStoreGetByVersionId resourceStoreGetByVersionId,
    IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IFhirDeSerializationSupport fhirDeSerializationSupport,
    IRepositoryEventCollector repositoryEventCollector)
    : IRequestHandler<FhirVersionReadRequest, FhirOptionalResourceResponse>
{
    public async Task<FhirOptionalResourceResponse> Handle(FhirVersionReadRequest request,
        CancellationToken cancellationToken)
    {
        ValidatorResult validatorResult = validator.Validate(request);
        if (!validatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(validatorResult);
        }
        
        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.ResourceName);

        int? historyIdAsInteger = GetHistoryIdAsInteger(request.HistoryId);
        if (!historyIdAsInteger.HasValue)
        {
            return NonIntegerHistoryIdResponse();
        }
        
        ResourceStore? resourceStore = await resourceStoreGetByVersionId.Get(
            resourceId: request.ResourceId, 
            versionId: historyIdAsInteger.Value, 
            resourceType: fhirResourceType);

        if (resourceStore is null)
        {
            return GetFhirOptionalResourceResponse(httpStatusCode: HttpStatusCode.NotFound);
        }
        
        var headers = fhirResponseHttpHeaderSupport.ForRead(
            lastUpdatedUtc: resourceStore.LastUpdatedUtc, 
            versionId: resourceStore.VersionId, 
            requestTimeStamp: request.TimeStamp);
    
        if (resourceStore.IsDeleted)
        {
            return GetFhirOptionalResourceResponse(httpStatusCode: HttpStatusCode.Gone);
        }

        AddRepositoryEvent(resourceStore.ResourceStoreId, request.RequestId);
        
        Resource? resource = fhirDeSerializationSupport.ToResource(resourceStore.Json);
        return new FhirOptionalResourceResponse(
            Resource: resource, 
            HttpStatusCode: HttpStatusCode.OK, 
            Headers: headers, 
            RepositoryEventCollector: repositoryEventCollector,
            ResourceOutcomeInfo: new ResourceOutcomeInfo(
                resourceId: resourceStore.ResourceId, 
                versionId: resourceStore.VersionId));
    }

    private FhirOptionalResourceResponse GetFhirOptionalResourceResponse(HttpStatusCode httpStatusCode)
    {
        repositoryEventCollector.Clear();
        return new FhirOptionalResourceResponse(
            Resource: null, 
            HttpStatusCode: httpStatusCode, 
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

    
    private static int? GetHistoryIdAsInteger(string historyId)
    {
        if (int.TryParse(historyId, out int historyIdInteger))
        {
            return historyIdInteger;
        }
        return null;
    }
    
    private FhirOptionalResourceResponse NonIntegerHistoryIdResponse()
    {
        //As this server only stores version ids as integers.
        //It is always true that a non-integer historyId will find no resource and therefore can return 'Not Found' without need to call the database
        return new FhirOptionalResourceResponse(
            Resource: null, 
            HttpStatusCode: HttpStatusCode.NotFound, 
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
    
    private void AddRepositoryEvent(int? resourceStoreId, string requestId)
    {
        ArgumentNullException.ThrowIfNull(resourceStoreId);
            
        repositoryEventCollector.Add(
            requestId: requestId,
            repositoryEventType: RepositoryEventType.Read, 
            resourceStoreId: resourceStoreId.Value);
    }
}