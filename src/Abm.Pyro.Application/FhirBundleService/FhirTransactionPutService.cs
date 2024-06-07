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

public class FhirTransactionPutService(
    IFhirBundleCommonSupport fhirBundleCommonSupport,
    IFhirUpdateHandler fhirUpdateHandler,
    IOperationOutcomeSupport operationOutcomeSupport,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IValidator validator,
    ISearchQueryService searchQueryService,
    IResourceStoreSearch resourceStoreSearch,
    IEndpointPolicyService endpointPolicyService,
    IResourceStoreGetForUpdateByResourceId resourceStoreGetForUpdateByResourceId,
    IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport)
    : IFhirTransactionPutService
{
    
    public async Task<OperationOutcome?> PreProcessPuts(List<Bundle.EntryComponent> entryList,
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
            
            Bundle.EntryComponent? putEntry = GetPutEntry(entryList[i]);
            if (putEntry is null)
            {
                continue; //Continue will cause the loop to immediately skip to the next entry in the loop, where as Break exits the loop
            }
            
            Result<FhirUri> fullUrlFhirUriResult = fhirBundleCommonSupport.ParseFhirUri(putEntry.FullUrl);
            if (fullUrlFhirUriResult.IsFailed)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"Unable to parse Bundle.entry[{i}].fullUrl of: {putEntry.FullUrl}. " + fullUrlFhirUriResult.Errors.First().Message
                });
            }
            if (!fullUrlFhirUriResult.Value.IsAbsoluteUri)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"The Bundle.entry[{i}].fullUrl of: {putEntry.FullUrl} must be either an absolute, UUID or OID resource reference. "
                });
            }
            FhirUri fullUrlFhirUri = fullUrlFhirUriResult.Value;
            
            Result<FhirUri> requestFhirUriResult = fhirBundleCommonSupport.ParseFhirUri(putEntry.Request.Url);
            if (requestFhirUriResult.IsFailed)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"Unable to parse Bundle.entry[{i}].request.url of: {putEntry.Request.Url}. " + requestFhirUriResult.Errors.First().Message
                });
            }
            FhirUri requestFhirUri = requestFhirUriResult.Value;
            
            var bundleEntryTransactionMetaData = new BundleEntryTransactionMetaData(forFullUrl: fullUrlFhirUri, requestUrl: requestFhirUri);
            if (!bundleEntryTransactionMetaDataDictionary.TryAdd(putEntry.FullUrl, bundleEntryTransactionMetaData))
            {
                //Ref: https://hl7.org/fhir/R4/http.html#trules
                //If any resource identities (including resolved identities from conditional update/delete) overlap in steps 1-3 (DELETE, POST, PUT), then the transaction SHALL fail.
                bundleEntryTransactionMetaData = bundleEntryTransactionMetaDataDictionary[putEntry.FullUrl];
                bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
                {
                    $"There are duplicate entries with the same fullUrl of: {putEntry.FullUrl} with in the Transaction Bundle, this is not allowed. "
                });
                break;
            }

            ValidateUpdateRequest(requestFhirUri, putEntry, bundleEntryTransactionMetaData);
            if (bundleEntryTransactionMetaData.IsFailure)
            {
                break;
            }
            
            if (IsConditionalPut(requestFhirUri))
            {
                if (!endpointPolicyService.GetEndpointPolicy(requestFhirUriResult.Value.ResourceName).AllowConditionalCreate)
                {
                    bundleEntryTransactionMetaData = bundleEntryTransactionMetaDataDictionary[putEntry.FullUrl];
                    bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
                    {
                        $"The entry with the fullUrl of: {putEntry.FullUrl} was unable to be committed as a conditional PUT action. " +
                        "(403 Forbidden) The server's endpoint policy controls have refused to authorize this request"
                    });
                    break;
                }
                
                await PreProcessConditionalUpdate(putEntry, requestHeaders, bundleEntryTransactionMetaData);
                if (bundleEntryTransactionMetaData.IsFailure)
                {
                    break;
                }
                continue;
            }
            
            if (!endpointPolicyService.GetEndpointPolicy(requestFhirUriResult.Value.ResourceName).AllowConditionalCreate)
            {
                bundleEntryTransactionMetaData = bundleEntryTransactionMetaDataDictionary[putEntry.FullUrl];
                bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
                {
                    $"The entry with the fullUrl of: {putEntry.FullUrl} was unable to be committed as a PUT action. " +
                    "(403 Forbidden) The server's endpoint policy controls have refused to authorize this request"
                });
                break;
            }
            
            await PreProcessUpdate(putEntry, bundleEntryTransactionMetaData, cancellationToken);
            if (bundleEntryTransactionMetaData.IsFailure)
            {
                break;
            }
        }

        return null;
    }

    public async Task ProcessPuts(
        List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, BundleEntryTransactionMetaData> transactionResourceActionOutcomeDictionary,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < entryList.Count(); i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            
            Bundle.EntryComponent? putEntry = GetPutEntry(entryList[i]);
            if (putEntry is null)
            {
                continue; //Continue will cause the loop to immediately skip to the next entry in the loop, whereas Break exits the loop
            }

            var transactionResourceActionOutcome = transactionResourceActionOutcomeDictionary[putEntry.FullUrl];

            ArgumentNullException.ThrowIfNull(transactionResourceActionOutcome.ResourceUpdateInfo);
            
            putEntry.Resource.Id = transactionResourceActionOutcome.ResourceUpdateInfo.NewResourceId;
            
            FhirOptionalResourceResponse updateResponse = await fhirUpdateHandler.Handle(
                resourceId: putEntry.Resource.Id,
                resource: putEntry.Resource,
                headers: GetPutRequestHeaders(putEntry, requestHeaders),
                cancellationToken: cancellationToken,
                previousResourceStore: transactionResourceActionOutcome.ResourceUpdateInfo.ResourceStoreUpdateProjection);

            if (HasUpdateRequestFailed(updateResponse, transactionResourceActionOutcome))
            {
                break;
            }
            
            putEntry.FullUrl = $"{transactionResourceActionOutcome.ForFullUrl.PrimaryServiceRootServers}{putEntry.Resource.TypeName}/{putEntry.Resource.Id}";
            putEntry.Resource = updateResponse.Resource;
            putEntry.Response = new Bundle.ResponseComponent
            {
                Status = updateResponse.HttpStatusCode.Display(),
                Etag = fhirBundleCommonSupport.GetHeaderValue(headers: updateResponse.Headers, headerName: HttpHeaderName.ETag),
                LastModified = fhirRequestHttpHeaderSupport.GetLastModified(updateResponse.Headers),
                Location = fhirBundleCommonSupport.GetHeaderValue(headers: updateResponse.Headers, headerName: HttpHeaderName.Location),
            };
        }
    }

    private async Task PreProcessConditionalUpdate(
        Bundle.EntryComponent putEntry, 
        Dictionary<string, StringValues> requestHeaders, 
        BundleEntryTransactionMetaData bundleEntryTransactionMetaData)
    {
        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(putEntry.Resource.TypeName);
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

     
        
        if (FhirConditionalUpdateHandler.IsMoreThanOneResourceMatch(resourceStoreSearchOutcome.SearchTotal))
        {
            //Multiple matches: The server returns a 412 Precondition Failed error indicating the client's criteria were not selective enough preferably with an OperationOutcome
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {putEntry.FullUrl} was unable to be committed as a PUT action. " +
                $"The conditional update search query found in the request.url returned a (412 Precondition Failed) response, indicating the criteria were not selective enough. "
            });
            return;
        }

        ResourceStore matchedResourceStore = resourceStoreSearchOutcome.ResourceStoreList.First();
        var resourceStoreUpdateProjection = new ResourceStoreUpdateProjection(
            resourceStoreId: matchedResourceStore.ResourceStoreId, 
            versionId: matchedResourceStore.VersionId, 
            isCurrent: matchedResourceStore.IsCurrent, 
            isDeleted: matchedResourceStore.IsDeleted);
        
        if (FhirConditionalUpdateHandler.IsSingleResourceMatch(resourceStoreSearchOutcome.SearchTotal) && FhirConditionalUpdateHandler.ResourceIdProvided(putEntry.Resource.Id) &&
            !FhirConditionalUpdateHandler.MatchedResourceIdEqualsProvidedResourcedId(putEntry.Resource.Id, matchedResourceStore.ResourceId))
        {
            //One Match, resource id provided but does not match resource found: The server returns a 400 Bad Request error indicating the client id
            //specification was a problem preferably with an OperationOutcome
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {putEntry.FullUrl} was unable to be committed as a PUT action. " +
                $"Conditional update criteria returned a single matched resource, however its resource id did not match the entry.resource's id. "
            });
            return;
        }

        if (FhirConditionalUpdateHandler.IsSingleResourceMatch(resourceStoreSearchOutcome.SearchTotal) && (!FhirConditionalUpdateHandler.ResourceIdProvided(putEntry.Resource.Id) ||
                                                                                                           FhirConditionalUpdateHandler.MatchedResourceIdEqualsProvidedResourcedId(putEntry.Resource.Id,
                                                                                                               matchedResourceStore.ResourceId)))
        {
            if (!FhirConditionalUpdateHandler.ResourceIdProvided(putEntry.Resource.Id))
            {
                putEntry.Resource.Id = matchedResourceStore.ResourceId;
            }
            
           
            
            //One Match, no resource id provided OR (resource id provided and it matches the found resource): The server performs the update against the matching resource
            bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
                ResourceName: putEntry.Resource.TypeName,
                NewResourceId: matchedResourceStore.ResourceId, 
                NewVersionId: matchedResourceStore.VersionId + 1, 
                CommittedResourceInfo: null,
                ResourceStoreUpdateProjection: resourceStoreUpdateProjection);
            return;
        }

        if (FhirConditionalUpdateHandler.NoResourceMatch(resourceStoreSearchOutcome.SearchTotal) && !FhirConditionalUpdateHandler.ResourceIdProvided(putEntry.Resource.Id))
        {
            //No matches, no id provided: The server creates the resource.
            bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
                ResourceName: putEntry.Resource.TypeName,
                NewResourceId: GuidSupport.NewFhirGuid(),
                NewVersionId: 1, 
                CommittedResourceInfo: null,
                ResourceStoreUpdateProjection: resourceStoreUpdateProjection);
            return;
        }

        if (FhirConditionalUpdateHandler.NoResourceMatch(resourceStoreSearchOutcome.SearchTotal) && FhirConditionalUpdateHandler.ResourceIdProvided(putEntry.Resource.Id))
        {
            //No matches, id provided: The server treats the interaction as an 'Update as Create' interaction (or rejects it, if 'Update as Create' not supported by the server)
            bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
                ResourceName: putEntry.Resource.TypeName,
                NewResourceId: putEntry.Resource.Id,
                NewVersionId: 1, 
                CommittedResourceInfo: null,
                ResourceStoreUpdateProjection: resourceStoreUpdateProjection);
            return;
        }
        
        throw new ApplicationException($"The Transaction Conditional Update has encountered and unknown action for the fullUrl of: {putEntry.FullUrl}.");
        
    }

    private async Task PreProcessUpdate(Bundle.EntryComponent putEntry,
        BundleEntryTransactionMetaData bundleEntryTransactionMetaData,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        
        if (!ResourceIdsAreEqual(putEntry.Resource.Id, bundleEntryTransactionMetaData.RequestUrl.ResourceId))
        {
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {putEntry.FullUrl} was unable to be committed as a PUT action. " +
                $"The entry.url resource id {putEntry.Resource.Id} did not equal the provided entry.resource's id {bundleEntryTransactionMetaData.RequestUrl.ResourceId}"
            });
            return;
        }
        
        if (!ResourceNamesAreEqual(bundleEntryTransactionMetaData.RequestUrl.ResourceName, putEntry.Resource.TypeName))
        {
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {putEntry.FullUrl} was unable to be committed as a PUT action. " +
                $"The entry.url resource type {putEntry.Resource.TypeName} did not equal the provided entry.resource's id {bundleEntryTransactionMetaData.RequestUrl.ResourceName}"
            });
            return;
        }

        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(bundleEntryTransactionMetaData.RequestUrl.ResourceName);
        ResourceStoreUpdateProjection? previousResourceStore = await resourceStoreGetForUpdateByResourceId.Get(fhirResourceType, bundleEntryTransactionMetaData.RequestUrl.ResourceId);
        
        if (previousResourceStore is null)
        {
            bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
                ResourceName: putEntry.Resource.TypeName,
                NewResourceId: GuidSupport.NewFhirGuid(),
                NewVersionId: 1,
                CommittedResourceInfo: null,
                ResourceStoreUpdateProjection: null);
            return;
        }
        
        bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
            ResourceName: putEntry.Resource.TypeName,
            NewResourceId: bundleEntryTransactionMetaData.RequestUrl.ResourceId,
            NewVersionId: previousResourceStore.VersionId + 1,
            CommittedResourceInfo: null,
            ResourceStoreUpdateProjection: previousResourceStore);
    }

    private void ValidateUpdateRequest(FhirUri requestFhirUri,
        Bundle.EntryComponent putEntry, BundleEntryTransactionMetaData metaData)
    {
        if (string.IsNullOrWhiteSpace(requestFhirUri.ResourceName))
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {putEntry.FullUrl} was unable to be committed as a PUT action. " +
                $"Unable to parse its request.url of: {putEntry.Request.Url}. " +
                $"No Resource name could be found."
            });
            return;
        }
        
        if (putEntry.Resource is null)
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {putEntry.FullUrl} was unable to be committed as a POST action. " +
                $"The entry.resource what found to be empty."
            }, metaData.FailureOperationOutcome);
            return;
        }
        
        if (!requestFhirUri.ResourceName.Equals(putEntry.Resource.TypeName))
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {putEntry.FullUrl} was unable to be committed as a PUT action. " +
                $"The Resource Type of {putEntry.Resource.TypeName} found in entry.resource did not match the Resource " +
                $"Type {requestFhirUri.ResourceName} found in the entry.request.url property."
            }, metaData.FailureOperationOutcome);
            return;
        }
    }

    private Dictionary<string, StringValues> GetPutRequestHeaders(Bundle.EntryComponent putEntry,
        Dictionary<string, StringValues> requestHeaders)
    {
        var postRequestHeaders = fhirRequestHttpHeaderSupport.GetRequestHeadersFromBundleEntryRequest(putEntry.Request);
        foreach (var requestHeader in requestHeaders)
        {
            if (!postRequestHeaders.ContainsKey(requestHeader.Key))
            {
                postRequestHeaders.Add(requestHeader.Key, requestHeader.Value);
            }
        }
        return postRequestHeaders;
    }

    private bool IsConditionalPut(FhirUri requestFhirUri)
    {
        return string.IsNullOrWhiteSpace(requestFhirUri.ResourceId) && !string.IsNullOrWhiteSpace(requestFhirUri.Query);
    }

    private static Bundle.EntryComponent? GetPutEntry(Bundle.EntryComponent entry)
    {
        if (entry.Request?.Method is not Bundle.HTTPVerb.PUT)
        {
            return null;
        }

        return entry;
    }

    private bool HasUpdateRequestFailed(FhirOptionalResourceResponse updateResponse, BundleEntryTransactionMetaData bundleEntryTransactionMetaData)
    {
        if (updateResponse.ResourceOutcomeInfo is not null)
        {
            return false;
        }
        
        if (updateResponse.Resource is OperationOutcome operationOutcome)
        {
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {bundleEntryTransactionMetaData.ForFullUrl.OriginalString} was unable to be committed as a PUT action. "
            }, operationOutcome: operationOutcome);
            return true;    
        }

        throw new ApplicationException($"When {nameof(updateResponse.ResourceOutcomeInfo)} is null, " +
                                       $"the {nameof(updateResponse.Resource)} is expected to be of type OperationOutcome");
    }

    private bool ResourceIdsAreEqual(string resourceIdA,
        string resourceIdB)
    {
        return (resourceIdA.Equals(resourceIdB, StringComparison.Ordinal));
    }

    private static bool ResourceNamesAreEqual(string resourceNameA, string resourceNameB)
    {
        return resourceNameA.Equals(resourceNameB);
    }
}