using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Validation;
using FluentResults;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;
using FhirUri = Abm.Pyro.Domain.FhirSupport.FhirUri;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirBundleService;

public class FhirTransactionDeleteService(
    IFhirBundleCommonSupport fhirBundleCommonSupport,
    IFhirDeleteHandler fhirDeleteHandler,
    IOperationOutcomeSupport operationOutcomeSupport,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IValidator validator,
    ISearchQueryService searchQueryService,
    IResourceStoreSearch resourceStoreSearch,
    IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport,
    IEndpointPolicyService endpointPolicyService)
    : IFhirTransactionDeleteService
{
    public async Task<OperationOutcome?> PreProcessDeletes(
        List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, BundleEntryTransactionMetaData> bundleEntryTransactionMetaDataDictionary,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < entryList.Count(); i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            Bundle.EntryComponent? deleteEntry = GetDeleteEntry(entryList[i]);
            if (deleteEntry is null)
            {
                continue; //Continue will cause the loop to immediately skip to the next entry in the loop, where as Break exits the loop
            }

            Result<FhirUri> fullUrlFhirUriResult = fhirBundleCommonSupport.ParseFhirUri(deleteEntry.FullUrl);
            if (fullUrlFhirUriResult.IsFailed)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"Unable to parse Bundle.entry[{i}].fullUrl of: {deleteEntry.FullUrl}. " + fullUrlFhirUriResult.Errors.First().Message
                });
            }
            
            if (!fullUrlFhirUriResult.Value.IsAbsoluteUri)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"The Bundle.entry[{i}].fullUrl of: {deleteEntry.FullUrl} must be either an absolute, UUID or OID resource reference. "
                });
            }
            
            FhirUri fullUrlFhirUri = fullUrlFhirUriResult.Value;

            Result<FhirUri> requestFhirUriResult = fhirBundleCommonSupport.ParseFhirUri(deleteEntry.Request.Url);
            if (requestFhirUriResult.IsFailed)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"Unable to parse Bundle.entry[{i}].request.url of: {deleteEntry.Request.Url}. " + requestFhirUriResult.Errors.First().Message
                });
            }

            FhirUri requestFhirUri = requestFhirUriResult.Value;

            var bundleEntryTransactionMetaData = new BundleEntryTransactionMetaData(forFullUrl: fullUrlFhirUri, requestUrl: requestFhirUri);
            if (!bundleEntryTransactionMetaDataDictionary.TryAdd(deleteEntry.FullUrl, bundleEntryTransactionMetaData))
            {
                //Ref: https://hl7.org/fhir/R4/http.html#trules
                //If any resource identities (including resolved identities from conditional update/delete) overlap in steps 1-3 (DELETE, POST, PUT), then the transaction SHALL fail.
                bundleEntryTransactionMetaData = bundleEntryTransactionMetaDataDictionary[deleteEntry.FullUrl];
                bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
                {
                    $"There are duplicate entries with the same fullUrl of: {deleteEntry.FullUrl} with in the Transaction Bundle, this is not allowed. "
                });
                break;
            }

            ValidateDeleteRequest(requestFhirUri, deleteEntry, bundleEntryTransactionMetaData);
            if (!bundleEntryTransactionMetaData.IsFailure)
            {
                break;
            }
            
            if (IfConditionalDelete(requestFhirUri))
            {
                if (!endpointPolicyService.GetEndpointPolicy(requestFhirUriResult.Value.ResourceName).AllowConditionalDelete)
                {
                    bundleEntryTransactionMetaData = bundleEntryTransactionMetaDataDictionary[deleteEntry.FullUrl];
                    bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
                    {
                        $"The entry with the fullUrl of: {deleteEntry.FullUrl} was unable to be committed as a conditional DELETE action. " +
                        "(403 Forbidden) The server's endpoint policy controls have refused to authorize this request"
                    });
                    break;
                }

                await PreProcessConditionalDelete(deleteEntry, requestHeaders, bundleEntryTransactionMetaData);
                if (bundleEntryTransactionMetaData.IsFailure)
                {
                    break;
                }
            }
            
            if (!endpointPolicyService.GetEndpointPolicy(requestFhirUriResult.Value.ResourceName).AllowDelete)
            {
                bundleEntryTransactionMetaData = bundleEntryTransactionMetaDataDictionary[deleteEntry.FullUrl];
                bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
                {
                    $"The entry with the fullUrl of: {deleteEntry.FullUrl} was unable to be committed as a DELETE action. " +
                    "(403 Forbidden) The server's endpoint policy controls have refused to authorize this request"
                });
                break;
            }
        }

        return null;
    }

    public async Task ProcessDelete(
        string tenant,
        List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, BundleEntryTransactionMetaData> bundleEntryTransactionMetaDataDictionary,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < entryList.Count(); i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            Bundle.EntryComponent? deleteEntry = GetDeleteEntry(entryList[i]);
            if (deleteEntry is null)
            {
                continue; //Continue will cause the loop to immediately skip to the next entry in the loop, whereas Break exits the loop
            }

            var transactionResourceActionOutcome = bundleEntryTransactionMetaDataDictionary[deleteEntry.FullUrl];

            FhirOptionalResourceResponse? deleteResponse;
            if (transactionResourceActionOutcome.ResourceUpdateInfo is not null)
            {
                deleteResponse = await fhirDeleteHandler.Handle(
                    tenant: tenant,
                    resourceName: transactionResourceActionOutcome.RequestUrl.ResourceName,
                    resourceId: transactionResourceActionOutcome.ResourceUpdateInfo
                        .NewResourceId, //Note that in this 'Conditional Delete' use case we set the found resource id in to the NewResourceId  
                    cancellationToken: cancellationToken,
                    previousResourceStore: transactionResourceActionOutcome.ResourceUpdateInfo.ResourceStoreUpdateProjection);
            }
            else
            {
                deleteResponse = await fhirDeleteHandler.Handle(
                    tenant: tenant,
                    resourceName: transactionResourceActionOutcome.RequestUrl.ResourceName,
                    resourceId: transactionResourceActionOutcome.RequestUrl.ResourceId,
                    cancellationToken: cancellationToken,
                    previousResourceStore: null);
            }

            ArgumentNullException.ThrowIfNull(deleteResponse);

            if (HasDeleteRequestFailed(deleteResponse, transactionResourceActionOutcome))
            {
                break;
            }

            deleteEntry.FullUrl = $"{transactionResourceActionOutcome.ForFullUrl.PrimaryServiceRootServers}{deleteEntry.Resource.TypeName}/{deleteEntry.Resource.Id}";
            deleteEntry.Resource = deleteResponse.Resource; //It will always be null in this case, but may be in future we may modify the response, so let it flow through here 
            deleteEntry.Response = new Bundle.ResponseComponent
            {
                Status = deleteResponse.HttpStatusCode.Display(),
                Etag = fhirBundleCommonSupport.GetHeaderValue(headers: deleteResponse.Headers, headerName: HttpHeaderName.ETag),
                LastModified = fhirRequestHttpHeaderSupport.GetLastModified(deleteResponse.Headers),
                Location = fhirBundleCommonSupport.GetHeaderValue(headers: deleteResponse.Headers, headerName: HttpHeaderName.Location),
            };
        }
    }

    private async Task PreProcessConditionalDelete(
        Bundle.EntryComponent deleteEntry,
        Dictionary<string, StringValues> requestHeaders,
        BundleEntryTransactionMetaData bundleEntryTransactionMetaData)
    {
        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(bundleEntryTransactionMetaData.RequestUrl.ResourceName);
        SearchQueryServiceOutcome searchQueryServiceOutcome = await searchQueryService.Process(fhirResourceType, bundleEntryTransactionMetaData.RequestUrl.Query);
        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            searchQueryServiceOutcome: searchQueryServiceOutcome,
            headers: requestHeaders));
        if (!searchQueryValidatorResult.IsValid)
        {
            bundleEntryTransactionMetaData.FailureOperationOutcome = searchQueryValidatorResult.GetOperationOutcome();
            return;
        }

        ResourceStoreSearchOutcome resourceStoreSearchOutcome = await resourceStoreSearch.GetSearch(searchQueryServiceOutcome);

        if (FhirConditionalDeleteHandler.IsMoreThanOneResourceMatch(resourceStoreSearchOutcome.SearchTotal))
        {
            //Multiple matches: The server returns a 412 Precondition Failed error indicating the client's criteria were not selective enough preferably with an OperationOutcome
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {deleteEntry.FullUrl} was unable to be committed as a PUT action. " +
                $"There was a (412 PreconditionFailed) response to the request.url Conditional Update search query, indicating the criteria were not selective enough. "
            });
            return;
        }

        ResourceStore matchedResourceStore = resourceStoreSearchOutcome.ResourceStoreList.First();
        if (FhirConditionalDeleteHandler.NoResourceMatch(resourceStoreSearchOutcome.SearchTotal))
        {
            //No matches : The server performs an ordinary delete action and returns 204 NoContent
            return;
        }

        if (FhirConditionalDeleteHandler.IsSingleResourceMatch(resourceStoreSearchOutcome.SearchTotal))
        {
            //No matches: The server performs an ordinary delete on the matching resource
            var resourceStoreUpdateProjection = new ResourceStoreUpdateProjection(
                resourceStoreId: matchedResourceStore.ResourceStoreId,
                versionId: matchedResourceStore.VersionId,
                isCurrent: matchedResourceStore.IsCurrent,
                isDeleted: matchedResourceStore.IsDeleted);

            bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
                ResourceName: matchedResourceStore.ResourceType.GetCode(),
                NewResourceId: matchedResourceStore.ResourceId,
                NewVersionId: matchedResourceStore.VersionId,
                CommittedResourceInfo: null,
                ResourceStoreUpdateProjection: resourceStoreUpdateProjection);
            return;
        }

        throw new ApplicationException($"The Transaction Conditional Delete has encountered and unknown action for the fullUrl of: {deleteEntry.FullUrl}. ");
    }

    private bool IfConditionalDelete(FhirUri requestFhirUri)
    {
        return string.IsNullOrWhiteSpace(requestFhirUri.ResourceId);
    }

    private void ValidateDeleteRequest(FhirUri requestFhirUri,
        Bundle.EntryComponent deleteEntry,
        BundleEntryTransactionMetaData metaData)
    {
        if (string.IsNullOrWhiteSpace(requestFhirUri.ResourceName))
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {deleteEntry.FullUrl} was unable to be committed as a DELETE action. " +
                $"Unable to parse its request.url of: {deleteEntry.Request.Url}. " +
                $"No Resource name could be found."
            });
            return;
        }

        if (deleteEntry.Resource is not null)
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {deleteEntry.FullUrl} was unable to be committed as a DELETE action. " +
                $"The entry.resource was found to be populated whereas the request.url is attempting to perform a DELETE action. " +
                $"The entry.resource must be empty when attempting to perform a DELETE action. "
            }, metaData.FailureOperationOutcome);
            return;
        }

        if (string.IsNullOrWhiteSpace(requestFhirUri.ResourceId) && string.IsNullOrWhiteSpace(requestFhirUri.Query))
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {deleteEntry.FullUrl} was unable to be committed as a DELETE action. " +
                $"The entry.url was found to not contains a Resource Id or a Search criteria. " +
                $"One or the other must be provided. "
            }, metaData.FailureOperationOutcome);
            return;
        }
    }

    private static Bundle.EntryComponent? GetDeleteEntry(Bundle.EntryComponent entry)
    {
        if (entry.Request?.Method is not Bundle.HTTPVerb.DELETE)
        {
            return null;
        }

        return entry;
    }

    private bool HasDeleteRequestFailed(FhirOptionalResourceResponse deleteResponse,
        BundleEntryTransactionMetaData bundleEntryTransactionMetaData)
    {
        if (deleteResponse.HttpStatusCode.Equals(HttpStatusCode.NoContent))
        {
            return false;
        }

        if (deleteResponse.Resource is OperationOutcome operationOutcome)
        {
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {bundleEntryTransactionMetaData.ForFullUrl.OriginalString} was unable to be committed as a Delete action. "
            }, operationOutcome: operationOutcome);
            return true;
        }

        throw new ApplicationException($"When {nameof(deleteResponse)} Http Status equal {HttpStatusCode.NoContent.ToString()}, " +
                                       $"the {nameof(deleteResponse.Resource)} is expected to be of type OperationOutcome");
    }
}