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
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;
using FhirUri = Abm.Pyro.Domain.FhirSupport.FhirUri;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirBundleService;

public class FhirTransactionPostService(
    IFhirBundleCommonSupport fhirBundleCommonSupport,
    IFhirCreateHandler fhirCreateHandler,
    IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport,
    IOperationOutcomeSupport operationOutcomeSupport,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IValidator validator,
    ISearchQueryService searchQueryService,
    IResourceStoreSearch resourceStoreSearch,
    IEndpointPolicyService endpointPolicyService,
    IFhirDeSerializationSupport fhirDeSerializationSupport,
    IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport)
    : IFhirTransactionPostService
{
    public async Task<OperationOutcome?> PreProcessPosts(List<Bundle.EntryComponent> entryList,
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
            
            Bundle.EntryComponent? postEntry = GetPostEntry(entryList[i]);
            if (postEntry is null)
            {
                continue; //Continue will cause the loop to immediately skip to the next entry in the loop, where as Break exits the loop
            }
            
            Result<FhirUri> fullUrlFhirUriResult = fhirBundleCommonSupport.ParseFhirUri(postEntry.FullUrl);
            if (fullUrlFhirUriResult.IsFailed)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"Unable to parse Bundle.entry[{i}].fullUrl of: {postEntry.FullUrl}. " + fullUrlFhirUriResult.Errors.First().Message
                });
            }
            if (!fullUrlFhirUriResult.Value.IsAbsoluteUri)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"The Bundle.entry[{i}].fullUrl of: {postEntry.FullUrl} must be either an absolute, UUID or OID resource reference. "
                });
            }
            FhirUri fullUrlFhirUri = fullUrlFhirUriResult.Value;
            
            Result<FhirUri> requestFhirUriResult = fhirBundleCommonSupport.ParseFhirUri(postEntry.Request.Url);
            if (requestFhirUriResult.IsFailed)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"Unable to parse Bundle.entry[{i}].request.url of: {postEntry.Request.Url}. " + requestFhirUriResult.Errors.First().Message
                });
            }
            FhirUri requestFhirUri = requestFhirUriResult.Value;
            
            var bundleEntryTransactionMetaData = new BundleEntryTransactionMetaData(forFullUrl: fullUrlFhirUri, requestUrl: requestFhirUri);
            if (!bundleEntryTransactionMetaDataDictionary.TryAdd(postEntry.FullUrl, bundleEntryTransactionMetaData))
            {
                //Ref: https://hl7.org/fhir/R4/http.html#trules
                //If any resource identities (including resolved identities from conditional update/delete) overlap in steps 1-3 (DELETE, POST, PUT), then the transaction SHALL fail.
                bundleEntryTransactionMetaData = bundleEntryTransactionMetaDataDictionary[postEntry.FullUrl];
                bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
                {
                    $"There are duplicate entries with the same fullUrl of: {postEntry.FullUrl} with in the Transaction Bundle, this is not allowed. "
                });
                break;
            }
            
            ValidateCreateRequest(requestFhirUri, postEntry, bundleEntryTransactionMetaData);
            if (bundleEntryTransactionMetaData.IsFailure)
            {
                break;
            }
            
            if (IfConditionalCreate(postEntry))
            {
                if (!endpointPolicyService.GetEndpointPolicy(requestFhirUriResult.Value.ResourceName).AllowConditionalCreate)
                {
                    bundleEntryTransactionMetaData = bundleEntryTransactionMetaDataDictionary[postEntry.FullUrl];
                    bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
                    {
                        $"The entry with the fullUrl of: {postEntry.FullUrl} was unable to be committed as a conditional POST action. " +
                        "(403 Forbidden) The server's endpoint policy controls have refused to authorize this request"
                    });
                    break;
                }
                
                await PreProcessConditionalCreate(postEntry, requestHeaders, bundleEntryTransactionMetaData);
                if (bundleEntryTransactionMetaData.IsFailure)
                {
                    break;
                }
                continue;
            }
                 
            if (!endpointPolicyService.GetEndpointPolicy(requestFhirUriResult.Value.ResourceName).AllowCreate)
            {
                bundleEntryTransactionMetaData = bundleEntryTransactionMetaDataDictionary[postEntry.FullUrl];
                bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
                {
                    $"The entry with the fullUrl of: {postEntry.FullUrl} was unable to be committed as a POST action. " +
                    "(403 Forbidden) The server's endpoint policy controls have refused to authorize this request"
                });
                break;
            }
            
            PreProcessCreate(bundleEntryTransactionMetaData, resourceName: postEntry.Resource.TypeName);
            if (bundleEntryTransactionMetaData.IsFailure)
            {
                break;
            }
        }

        return null;
    }

    public async Task ProcessPosts(
        string tenant,
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
            
            Bundle.EntryComponent? postEntry = GetPostEntry(entryList[i]);
            if (postEntry is null)
            {
                continue; //Continue will cause the loop to immediately skip to the next entry in the loop, whereas Break exits the loop
            }

            var transactionResourceActionOutcome = transactionResourceActionOutcomeDictionary[postEntry.FullUrl];

            ArgumentNullException.ThrowIfNull(transactionResourceActionOutcome.ResourceUpdateInfo);
            
            postEntry.Resource.Id = transactionResourceActionOutcome.ResourceUpdateInfo.NewResourceId;

            //This is the case where the preprocessing Condition Create search criteria matched
            //one resource instance therefore the server ignores the post and returns 200 OK
            if (transactionResourceActionOutcome.ResourceUpdateInfo.CommittedResourceInfo is not null)
            {
                postEntry.FullUrl = $"{transactionResourceActionOutcome.ForFullUrl.PrimaryServiceRootServers}/{postEntry.Resource.TypeName}/{postEntry.Resource.Id}";
                postEntry.Resource = transactionResourceActionOutcome.ResourceUpdateInfo.CommittedResourceInfo.Resource;
                postEntry.Response = new Bundle.ResponseComponent
                {
                    Status = HttpStatusCode.OK.Display(),
                    Etag = fhirBundleCommonSupport.GetHeaderValue(headers: transactionResourceActionOutcome.ResourceUpdateInfo.CommittedResourceInfo.Headers, headerName: HttpHeaderName.ETag),
                    LastModified = fhirRequestHttpHeaderSupport.GetLastModified(transactionResourceActionOutcome.ResourceUpdateInfo.CommittedResourceInfo.Headers)
                };
                continue;
            }
            
            FhirOptionalResourceResponse postResponse = await fhirCreateHandler.Handle(
                tenant: tenant,
                resourceId: postEntry.Resource.Id,
                resource: postEntry.Resource,
                headers: GetPostRequestHeaders(postEntry, requestHeaders),
                cancellationToken: cancellationToken);

            if (HasCreateRequestFailed(postResponse, transactionResourceActionOutcome))
            {
                break;
            }
            
            postEntry.FullUrl = $"{transactionResourceActionOutcome.ForFullUrl.PrimaryServiceRootServers}{postEntry.Resource.TypeName}/{postEntry.Resource.Id}";
            postEntry.Resource = postResponse.Resource;
            postEntry.Response = new Bundle.ResponseComponent
            {
                Status = postResponse.HttpStatusCode.Display(),
                Etag = fhirBundleCommonSupport.GetHeaderValue(headers: postResponse.Headers, headerName: HttpHeaderName.ETag),
                LastModified = fhirRequestHttpHeaderSupport.GetLastModified(postResponse.Headers),
                Location = fhirBundleCommonSupport.GetHeaderValue(headers: postResponse.Headers, headerName: HttpHeaderName.Location),
            };
            
        }
    }

    private async Task PreProcessConditionalCreate(
        Bundle.EntryComponent postEntry, 
        Dictionary<string, StringValues> requestHeaders, 
        BundleEntryTransactionMetaData bundleEntryTransactionMetaData)
    {
        ArgumentNullException.ThrowIfNull(postEntry.Request?.IfNoneExist);
        
        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(postEntry.Resource.TypeName);
        SearchQueryServiceOutcome searchQueryServiceOutcome = await searchQueryService.Process(fhirResourceType, postEntry.Request?.IfNoneExist);
        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            searchQueryServiceOutcome: searchQueryServiceOutcome, 
            headers: requestHeaders));
        if (!searchQueryValidatorResult.IsValid)
        {
            bundleEntryTransactionMetaData.FailureOperationOutcome = searchQueryValidatorResult.GetOperationOutcome();
            return;
        }
        
        ResourceStoreSearchOutcome resourceStoreSearchOutcome = await resourceStoreSearch.GetSearch(searchQueryServiceOutcome);
        if (FhirConditionalCreateHandler.NoMatches(resourceStoreSearchOutcome))
        {
            //No matches: The server processes the create as a normal Create
            var resourceStore = resourceStoreSearchOutcome.ResourceStoreList.First();
            bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
                NewResourceId: GuidSupport.NewFhirGuid(),
                ResourceName: postEntry.Resource.TypeName,
                NewVersionId: 1,
                CommittedResourceInfo: null,
                ResourceStoreUpdateProjection: null);
            return;
        }

        if (FhirConditionalCreateHandler.OneMatch(resourceStoreSearchOutcome))
        {
            //One Match: The server ignores the post and returns 200 OK
            var resourceStore = resourceStoreSearchOutcome.ResourceStoreList.First();
            Resource? resource = fhirDeSerializationSupport.ToResource(resourceStore.Json);
            ArgumentNullException.ThrowIfNull(resource);

            var headers = fhirResponseHttpHeaderSupport.ForRead(
                lastUpdatedUtc: resourceStore.LastUpdatedUtc,
                versionId: resourceStore.VersionId,
                requestTimeStamp: DateTimeOffset.Now); //Note requestTimeStamp is not used here 
            
            bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
                ResourceName: resourceStore.ResourceType.GetCode(),
                NewResourceId: resourceStore.ResourceId,
                NewVersionId: resourceStore.VersionId,
                CommittedResourceInfo: new CommittedResourceInfo(
                    Resource: resource, 
                    Headers: headers),
                ResourceStoreUpdateProjection: null);
            return;
        }

        if (FhirConditionalCreateHandler.MultipleMatches(resourceStoreSearchOutcome))
        {
            //Multiple matches: The server returns a 412 Precondition Failed error indicating the client's criteria were not selective enough
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {postEntry.FullUrl} was unable to be committed as a POST action. " +
                $"There was a (412 PreconditionFailed) response to the request.IfNoneExist Conditional Create search query, indicating the criteria were not selective enough. "
            });
            return;
        }
        
        throw new ApplicationException($"The Transaction Conditional Create has encountered and unknown action for the fullUrl of: {postEntry.FullUrl}. ");
    }

    private void PreProcessCreate(BundleEntryTransactionMetaData bundleEntryTransactionMetaData, string resourceName)
    {
        bundleEntryTransactionMetaData.ResourceUpdateInfo = new ResourceUpdateInfo(
            ResourceName: resourceName,
            NewResourceId: GuidSupport.NewFhirGuid(),
            NewVersionId: 1,
            CommittedResourceInfo: null,
            ResourceStoreUpdateProjection: null);
    }

    private void ValidateCreateRequest(FhirUri requestFhirUri,
        Bundle.EntryComponent postEntry, BundleEntryTransactionMetaData metaData)
    {
        if (string.IsNullOrWhiteSpace(requestFhirUri.ResourceName))
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {postEntry.FullUrl} was unable to be committed as a POST action. " +
                $"Unable to parse its request.url of: {postEntry.Request.Url}. " +
                $"No Resource name could be found."
            });
            return;
        }
        
        if (postEntry.Resource is null)
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {postEntry.FullUrl} was unable to be committed as a POST action. " +
                $"The entry.resource what found to be empty."
            }, metaData.FailureOperationOutcome);
            return;
        }
        
        if (!requestFhirUri.ResourceName.Equals(postEntry.Resource.TypeName))
        {
            metaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {postEntry.FullUrl} was unable to be committed as a POST action. " +
                $"The resource type of {postEntry.Resource.TypeName} found in entry.resource did not match the resource " +
                $"type {requestFhirUri.ResourceName} found in the entry.request.url property."
            }, metaData.FailureOperationOutcome);
            return;
        }
    }       

    private bool HasCreateRequestFailed(FhirOptionalResourceResponse postResponse, BundleEntryTransactionMetaData bundleEntryTransactionMetaData)
    {
        if (postResponse.ResourceOutcomeInfo is not null)
        {
            return false;
        }
        
        if (postResponse.Resource is OperationOutcome operationOutcome)
        {
            bundleEntryTransactionMetaData.FailureOperationOutcome = operationOutcomeSupport.GetError(new[]
            {
                $"The entry with the fullUrl of: {bundleEntryTransactionMetaData.ForFullUrl.OriginalString} was unable to be committed as a POST action. "
            }, operationOutcome: operationOutcome);
            return true;    
        }

        throw new ApplicationException($"When {nameof(postResponse.ResourceOutcomeInfo)} is null, " +
                                       $"the {nameof(postResponse.Resource)} is expected to be of type OperationOutcome");
    }
    
    private bool IfConditionalCreate(Bundle.EntryComponent postEntry)
    {
        
        return !string.IsNullOrWhiteSpace(postEntry.Request?.IfNoneExist);
    }

    private Dictionary<string, StringValues> GetPostRequestHeaders(Bundle.EntryComponent postEntry,
        Dictionary<string, StringValues> requestHeaders)
    {
        var postRequestHeaders = fhirRequestHttpHeaderSupport.GetRequestHeadersFromBundleEntryRequest(postEntry.Request);
        foreach (var requestHeader in requestHeaders)
        {
            if (!postRequestHeaders.ContainsKey(requestHeader.Key))
            {
                postRequestHeaders.Add(requestHeader.Key, requestHeader.Value);
            }
        }
        return postRequestHeaders;
    }

    private static Bundle.EntryComponent? GetPostEntry(Bundle.EntryComponent entry)
    {
        if (entry.Request?.Method is not Bundle.HTTPVerb.POST)
        {
            return null;
        }

        return entry;
    }
    
}