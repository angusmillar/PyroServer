using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Application.Validation;
using FluentResults;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirBundleService;
using Abm.Pyro.Domain.FhirResponse;
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

public class FhirTransactionPutAndPatchService(
    ITenantService tenantService,
    IFhirBundleCommonSupport fhirBundleCommonSupport,
    IFhirUpdateHandler fhirUpdateHandler,
    IFhirPatchHandler fhirPatchHandler,
    IOperationOutcomeSupport operationOutcomeSupport,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IValidator validator,
    ISearchQueryService searchQueryService,
    IResourceStoreSearch resourceStoreSearch,
    IEndpointPolicyService endpointPolicyService,
    IResourceStoreGetForUpdateByResourceId resourceStoreGetForUpdateByResourceId,
    IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport)
    : IFhirTransactionPutAndPatchService
{

    public async Task<OperationOutcome?> PreProcessPutsAndPatches(List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        Dictionary<string, BundleEntryTransactionMetaData> bundleEntryTransactionMetaDataDictionary,
        CancellationToken cancellationToken)
    {
        var resolvedIdentitySet = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < entryList.Count(); i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            Bundle.EntryComponent? entry = GetPutOrPatchEntry(entryList[i]);
            if (entry is null)
            {
                continue; //Continue will cause the loop to immediately skip to the next entry in the loop, where as Break exits the loop
            }

            Result<FhirUri> fullUrlFhirUriResult = fhirBundleCommonSupport.ParseFhirUri(entry.FullUrl);
            if (fullUrlFhirUriResult.IsFailed)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"Unable to parse Bundle.entry[{i}].fullUrl of: {entry.FullUrl}. " + fullUrlFhirUriResult.Errors.First().Message
                });
            }
            FhirUri fullUrlFhirUri = fullUrlFhirUriResult.Value;

            Result<FhirUri> requestFhirUriResult = fhirBundleCommonSupport.ParseFhirUri(entry.Request.Url);
            if (requestFhirUriResult.IsFailed)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"Unable to parse Bundle.entry[{i}].request.url of: {entry.Request.Url}. " + requestFhirUriResult.Errors.First().Message
                });
            }
            FhirUri requestFhirUri = requestFhirUriResult.Value;

            var bundleEntryTransactionMetaData = new BundleEntryTransactionMetaData(forFullUrl: fullUrlFhirUri, requestUrl: requestFhirUri);
            if (!bundleEntryTransactionMetaDataDictionary.TryAdd(entry.FullUrl, bundleEntryTransactionMetaData))
            {
                //Ref: https://hl7.org/fhir/R4/http.html#trules
                //If any resource identities (including resolved identities from conditional update/delete) overlap in steps 1-3 (DELETE, POST, PUT), then the transaction SHALL fail.
                bundleEntryTransactionMetaData = bundleEntryTransactionMetaDataDictionary[entry.FullUrl];
                bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
                {
                    $"There are duplicate entries with the same fullUrl of: {entry.FullUrl} with in the Transaction Bundle, this is not allowed. "
                });
                break;
            }

            if (entry.Request.Method is Bundle.HTTPVerb.PATCH)
            {
                await PreProcessPatchEntry(entry, requestFhirUri, requestHeaders, bundleEntryTransactionMetaData, cancellationToken);
            }
            else
            {
                await PreProcessPutEntry(entry, requestFhirUri, requestHeaders, bundleEntryTransactionMetaData, cancellationToken);
            }

            if (bundleEntryTransactionMetaData.IsFailure)
            {
                break;
            }

            if (bundleEntryTransactionMetaData.ResourceUpdateInfo is not null)
            {
                //Ref: https://hl7.org/fhir/R4/http.html#trules
                //If any resource identities (including resolved identities from conditional update/delete) overlap in steps 1-3 (DELETE, POST, PUT), then the transaction SHALL fail.
                string resolvedIdentity = $"{bundleEntryTransactionMetaData.ResourceUpdateInfo.ResourceName}/{bundleEntryTransactionMetaData.ResourceUpdateInfo.NewResourceId}";
                if (!resolvedIdentitySet.Add(resolvedIdentity))
                {
                    bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
                    {
                        $"The entry with the fullUrl of: {entry.FullUrl} was unable to be committed. " +
                        $"The resource identity {resolvedIdentity} is the target of more than one PUT or PATCH entry within the Transaction Bundle; " +
                        $"per https://hl7.org/fhir/R4/http.html#trules overlapping resource identities SHALL fail the transaction. "
                    });
                    break;
                }
            }
        }

        return null;
    }

    public async Task ProcessPutsAndPatches(
        string tenant,
        string requestId,
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

            Bundle.EntryComponent? entry = GetPutOrPatchEntry(entryList[i]);
            if (entry is null)
            {
                continue; //Continue will cause the loop to immediately skip to the next entry in the loop, whereas Break exits the loop
            }

            var transactionResourceActionOutcome = transactionResourceActionOutcomeDictionary[entry.FullUrl];

            ArgumentNullException.ThrowIfNull(transactionResourceActionOutcome.ResourceUpdateInfo);

            FhirOptionalResourceResponse response;
            string verbName;

            if (entry.Request.Method is Bundle.HTTPVerb.PATCH)
            {
                verbName = "PATCH";

                if (entry.Resource is not Parameters patchParameters)
                {
                    throw new ApplicationException(
                        $"The Bundle entry with the fullUrl of: {entry.FullUrl} was pre-processed as a PATCH action, " +
                        $"but its entry.resource was not of type Parameters at commit time.");
                }

                response = await fhirPatchHandler.Handle(
                    tenant: tenant,
                    requestId: requestId,
                    resourceId: transactionResourceActionOutcome.ResourceUpdateInfo.NewResourceId,
                    resourceName: transactionResourceActionOutcome.ResourceUpdateInfo.ResourceName,
                    patchParameters: patchParameters,
                    headers: GetEntryRequestHeaders(entry, requestHeaders),
                    cancellationToken: cancellationToken,
                    previousResourceStore: transactionResourceActionOutcome.ResourceUpdateInfo.ResourceStoreUpdateProjection);
            }
            else
            {
                verbName = "PUT";
                entry.Resource.Id = transactionResourceActionOutcome.ResourceUpdateInfo.NewResourceId;

                response = await fhirUpdateHandler.Handle(
                    tenant: tenant,
                    requestId: requestId,
                    resourceId: entry.Resource.Id,
                    resource: entry.Resource,
                    headers: GetEntryRequestHeaders(entry, requestHeaders),
                    cancellationToken: cancellationToken,
                    previousResourceStore: transactionResourceActionOutcome.ResourceUpdateInfo.ResourceStoreUpdateProjection);
            }

            if (HasRequestFailed(response, transactionResourceActionOutcome, verbName))
            {
                break;
            }

            entry.FullUrl = $"{transactionResourceActionOutcome.ForFullUrl.PrimaryServiceRootServers}/{transactionResourceActionOutcome.ResourceUpdateInfo.ResourceName}/{transactionResourceActionOutcome.ResourceUpdateInfo.NewResourceId}";
            entry.Resource = response.Resource;
            entry.Response = new Bundle.ResponseComponent
            {
                Status = response.HttpStatusCode.Display(),
                Etag = fhirBundleCommonSupport.GetHeaderValue(headers: response.Headers, headerName: HttpHeaderName.ETag),
                LastModified = fhirRequestHttpHeaderSupport.GetLastModified(response.Headers),
                Location = fhirBundleCommonSupport.GetHeaderValue(headers: response.Headers, headerName: HttpHeaderName.Location),
            };
        }
    }

    private async Task PreProcessPutEntry(
        Bundle.EntryComponent putEntry,
        FhirUri requestFhirUri,
        Dictionary<string, StringValues> requestHeaders,
        BundleEntryTransactionMetaData bundleEntryTransactionMetaData,
        CancellationToken cancellationToken)
    {
        ValidateUpdateRequest(requestFhirUri, putEntry, bundleEntryTransactionMetaData);
        if (bundleEntryTransactionMetaData.IsFailure)
        {
            return;
        }

        if (IsConditionalRequest(requestFhirUri))
        {
            if (!endpointPolicyService.GetEndpointPolicy(tenantService.GetScopedTenantCode(), requestFhirUri.ResourceName).AllowConditionalUpdate)
            {
                bundleEntryTransactionMetaData.FailureOperationOutcome = GetEndpointPolicyRefusedFailure(putEntry.FullUrl, "conditional PUT");
                return;
            }

            await PreProcessConditionalUpdate(putEntry, requestHeaders, bundleEntryTransactionMetaData);
            return;
        }

        if (!endpointPolicyService.GetEndpointPolicy(tenantService.GetScopedTenantCode(), requestFhirUri.ResourceName).AllowUpdate)
        {
            bundleEntryTransactionMetaData.FailureOperationOutcome = GetEndpointPolicyRefusedFailure(putEntry.FullUrl, "PUT");
            return;
        }

        await PreProcessUpdate(putEntry, bundleEntryTransactionMetaData, cancellationToken);
    }

    private async Task PreProcessPatchEntry(
        Bundle.EntryComponent patchEntry,
        FhirUri requestFhirUri,
        Dictionary<string, StringValues> requestHeaders,
        BundleEntryTransactionMetaData bundleEntryTransactionMetaData,
        CancellationToken cancellationToken)
    {
        ValidatePatchRequest(requestFhirUri, patchEntry, bundleEntryTransactionMetaData);
        if (bundleEntryTransactionMetaData.IsFailure)
        {
            return;
        }

        if (IsConditionalRequest(requestFhirUri))
        {
            if (!endpointPolicyService.GetEndpointPolicy(tenantService.GetScopedTenantCode(), requestFhirUri.ResourceName).AllowConditionalPatch)
            {
                bundleEntryTransactionMetaData.FailureOperationOutcome = GetEndpointPolicyRefusedFailure(patchEntry.FullUrl, "conditional PATCH");
                return;
            }

            await PreProcessConditionalPatch(patchEntry, requestFhirUri, requestHeaders, bundleEntryTransactionMetaData);
            return;
        }

        if (!endpointPolicyService.GetEndpointPolicy(tenantService.GetScopedTenantCode(), requestFhirUri.ResourceName).AllowPatch)
        {
            bundleEntryTransactionMetaData.FailureOperationOutcome = GetEndpointPolicyRefusedFailure(patchEntry.FullUrl, "PATCH");
            return;
        }

        await PreProcessDirectPatch(patchEntry, requestFhirUri, bundleEntryTransactionMetaData, cancellationToken);
    }

    private async Task PreProcessConditionalUpdate(
        Bundle.EntryComponent putEntry,
        Dictionary<string, StringValues> requestHeaders,
        BundleEntryTransactionMetaData bundleEntryTransactionMetaData)
    {
        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(putEntry.Resource.TypeName);
        SearchQueryServiceOutcome searchQueryServiceOutcome = await searchQueryService.Process(fhirResourceType, bundleEntryTransactionMetaData.RequestUrl.Query);
        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            SearchQueryServiceOutcome: searchQueryServiceOutcome,
            Headers: requestHeaders));
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




        if (FhirConditionalUpdateHandler.NoResourceMatch(resourceStoreSearchOutcome.SearchTotal) && !FhirConditionalUpdateHandler.ResourceIdProvided(putEntry.Resource.Id))
        {
            //No matches, no id provided: The server creates the resource.
            bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
                ResourceName: putEntry.Resource.TypeName,
                NewResourceId: GuidSupport.NewFhirGuid(),
                NewVersionId: 1,
                CommittedResourceInfo: null,
                ResourceStoreUpdateProjection: null);
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
                ResourceStoreUpdateProjection: null);
            return;
        }

        ResourceStore matchedResourceStore = resourceStoreSearchOutcome.ResourceStoreList.First();


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
                ResourceStoreUpdateProjection: new ResourceStoreUpdateProjection(
                    resourceStoreId: matchedResourceStore.ResourceStoreId,
                    versionId: matchedResourceStore.VersionId,
                    isCurrent: matchedResourceStore.IsCurrent,
                    isDeleted: matchedResourceStore.IsDeleted));
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
                NewResourceId: bundleEntryTransactionMetaData.RequestUrl.ResourceId,
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

    private async Task PreProcessConditionalPatch(
        Bundle.EntryComponent patchEntry,
        FhirUri requestFhirUri,
        Dictionary<string, StringValues> requestHeaders,
        BundleEntryTransactionMetaData bundleEntryTransactionMetaData)
    {
        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(requestFhirUri.ResourceName);
        SearchQueryServiceOutcome searchQueryServiceOutcome = await searchQueryService.Process(fhirResourceType, requestFhirUri.Query);
        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            SearchQueryServiceOutcome: searchQueryServiceOutcome,
            Headers: requestHeaders));
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
                $"The entry with the fullUrl of: {patchEntry.FullUrl} was unable to be committed as a PATCH action. " +
                $"The conditional patch search query found in the request.url returned a (412 Precondition Failed) response, indicating the criteria were not selective enough. "
            });
            return;
        }

        if (FhirConditionalUpdateHandler.NoResourceMatch(resourceStoreSearchOutcome.SearchTotal))
        {
            //No matches: PATCH never creates resources, unlike PUT.
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {patchEntry.FullUrl} was unable to be committed as a PATCH action. " +
                $"(404 Not Found) The conditional patch search query found in the request.url found no matches. PATCH does not create resources — use POST or PUT. "
            });
            return;
        }

        ResourceStore matchedResourceStore = resourceStoreSearchOutcome.ResourceStoreList.First();

        //Exactly one match: The server performs the patch against the matching resource
        bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
            ResourceName: requestFhirUri.ResourceName,
            NewResourceId: matchedResourceStore.ResourceId,
            NewVersionId: matchedResourceStore.VersionId + 1,
            CommittedResourceInfo: null,
            ResourceStoreUpdateProjection: new ResourceStoreUpdateProjection(
                resourceStoreId: matchedResourceStore.ResourceStoreId,
                versionId: matchedResourceStore.VersionId,
                isCurrent: matchedResourceStore.IsCurrent,
                isDeleted: matchedResourceStore.IsDeleted));
    }

    private async Task PreProcessDirectPatch(
        Bundle.EntryComponent patchEntry,
        FhirUri requestFhirUri,
        BundleEntryTransactionMetaData bundleEntryTransactionMetaData,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(requestFhirUri.ResourceName);
        ResourceStoreUpdateProjection? previousResourceStore = await resourceStoreGetForUpdateByResourceId.Get(fhirResourceType, requestFhirUri.ResourceId);

        //PATCH never creates — 404 when the target resource does not exist or has been deleted
        if (previousResourceStore is null || previousResourceStore.IsDeleted)
        {
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {patchEntry.FullUrl} was unable to be committed as a PATCH action. " +
                $"(404 Not Found) No resource of type {requestFhirUri.ResourceName} was found with the id: {requestFhirUri.ResourceId}. PATCH does not create resources — use POST or PUT. "
            });
            return;
        }

        bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
            ResourceName: requestFhirUri.ResourceName,
            NewResourceId: requestFhirUri.ResourceId,
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
                $"The entry with the fullUrl of: {putEntry.FullUrl} was unable to be committed as a PUT action. " +
                $"The entry.resource was found to be empty."
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

    private void ValidatePatchRequest(FhirUri requestFhirUri,
        Bundle.EntryComponent patchEntry, BundleEntryTransactionMetaData metaData)
    {
        if (string.IsNullOrWhiteSpace(requestFhirUri.ResourceName))
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {patchEntry.FullUrl} was unable to be committed as a PATCH action. " +
                $"Unable to parse its request.url of: {patchEntry.Request.Url}. " +
                $"No Resource name could be found."
            });
            return;
        }

        if (patchEntry.Resource is not Parameters)
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {patchEntry.FullUrl} was unable to be committed as a PATCH action. " +
                $"The body of a PATCH request must be a FHIR Parameters resource (resourceType 'Parameters'), but received '{patchEntry.Resource?.TypeName ?? "null"}'."
            }, metaData.FailureOperationOutcome);
            return;
        }

        if (string.IsNullOrWhiteSpace(requestFhirUri.ResourceId) && string.IsNullOrWhiteSpace(requestFhirUri.Query))
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {patchEntry.FullUrl} was unable to be committed as a PATCH action. " +
                $"The entry.request.url must be either a direct PATCH (ResourceType/id) or a conditional PATCH (ResourceType?criteria)."
            }, metaData.FailureOperationOutcome);
            return;
        }
    }

    private OperationOutcome GetEndpointPolicyRefusedFailure(string fullUrl, string verbName)
    {
        return operationOutcomeSupport.GetError(new[]
        {
            $"The entry with the fullUrl of: {fullUrl} was unable to be committed as a {verbName} action. " +
            "(403 Forbidden) The server's endpoint policy controls have refused to authorize this request"
        });
    }

    private Dictionary<string, StringValues> GetEntryRequestHeaders(Bundle.EntryComponent entry,
        Dictionary<string, StringValues> requestHeaders)
    {
        var entryRequestHeaders = fhirRequestHttpHeaderSupport.GetRequestHeadersFromBundleEntryRequest(entry.Request);
        foreach (var requestHeader in requestHeaders)
        {
            if (!entryRequestHeaders.ContainsKey(requestHeader.Key))
            {
                entryRequestHeaders.Add(requestHeader.Key, requestHeader.Value);
            }
        }
        return entryRequestHeaders;
    }

    private bool IsConditionalRequest(FhirUri requestFhirUri)
    {
        return string.IsNullOrWhiteSpace(requestFhirUri.ResourceId) && !string.IsNullOrWhiteSpace(requestFhirUri.Query);
    }

    private static Bundle.EntryComponent? GetPutOrPatchEntry(Bundle.EntryComponent entry)
    {
        if (entry.Request?.Method is Bundle.HTTPVerb.PUT or Bundle.HTTPVerb.PATCH)
        {
            return entry;
        }

        return null;
    }

    private bool HasRequestFailed(FhirOptionalResourceResponse response, BundleEntryTransactionMetaData bundleEntryTransactionMetaData, string verbName)
    {
        if (response.ResourceOutcomeInfo is not null)
        {
            return false;
        }

        if (response.Resource is OperationOutcome operationOutcome)
        {
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {bundleEntryTransactionMetaData.ForFullUrl.OriginalString} was unable to be committed as a {verbName} action. "
            }, operationOutcome: operationOutcome);
            return true;
        }

        throw new ApplicationException($"When {nameof(response.ResourceOutcomeInfo)} is null, " +
                                       $"the {nameof(response.Resource)} is expected to be of type OperationOutcome");
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
