using System.Net;
using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.FhirResponse;
using FluentResults;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Support;
using FhirUri = Abm.Pyro.Domain.FhirSupport.FhirUri;

namespace Abm.Pyro.Application.FhirBundleService;

public class FhirTransactionGetService(
    IFhirBundleCommonSupport fhirBundleCommonSupport,
    IFhirReadHandler fhirReadHandler,
    IFhirSearchHandler fhirSearchHandler,
    IEndpointPolicyService endpointPolicyService,
    IFhirRequestHttpHeaderSupport fhirRequestHttpHeaderSupport,
    IOperationOutcomeSupport operationOutcomeSupport)
    : IFhirTransactionGetService
{
    public async Task<OperationOutcome?> ProcessGets(
        List<Bundle.EntryComponent> entryList,
        Dictionary<string, StringValues> requestHeaders,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < entryList.Count(); i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            
            Bundle.EntryComponent? getEntry = GetGetEntry(entryList[i]);
            if (getEntry is null)
            {
                continue; //Continue will cause the loop to immediately skip to the next entry in the loop, whereas Break exits the loop
            }
            
            OperationOutcome? entityRequestIsNullOperationOutcome = IsEntityRequestNull(getEntry: getEntry, entityCounter: i);
            if (entityRequestIsNullOperationOutcome is not null)
            {
                return entityRequestIsNullOperationOutcome;
            }
            
            Result<FhirUri> requestFhirUriResult = fhirBundleCommonSupport.ParseFhirUri(getEntry.Request.Url);
            if (requestFhirUriResult.IsFailed)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"Unable to parse Bundle.entry[{(i + 1).ToString()}].request.url of {getEntry.Request.Url} "
                });
            }
            FhirUri requestFhirUri = requestFhirUriResult.Value;
            
            if (IsSearchRequest(requestFhirUri))
            {
                if (!endpointPolicyService.GetEndpointPolicy(requestFhirUriResult.Value.ResourceName).AllowSearch)
                {
                    return operationOutcomeSupport.GetError(new[]
                    {
                        $"The entry with the fullUrl of: {getEntry.FullUrl} was unable to be committed as a SEARCH action. " +
                        "(403 Forbidden) The server's endpoint policy controls have refused to authorize this request"
                    });
                }
                
                FhirResourceResponse searchResponse = await fhirSearchHandler.Handle(
                    ResourceName: requestFhirUri.ResourceName,
                    query: requestFhirUri.Query,
                    headers: GetRequestHeaders(getEntry: getEntry, requestHeaders: requestHeaders),
                    cancellationToken: cancellationToken);
                
                OperationOutcome? failedSearchOperationOutcome = HasSearchRequestFailed(searchResponse: searchResponse, entityCounter: i);
                if (failedSearchOperationOutcome is not null)
                {
                    return failedSearchOperationOutcome;
                }
                
                getEntry.FullUrl = $"urn:uuid:{searchResponse.Resource.Id}";
                getEntry.Resource = searchResponse.Resource;
                getEntry.Response = new Bundle.ResponseComponent
                {
                    Status = searchResponse.HttpStatusCode.Display(),
                    Etag = fhirBundleCommonSupport.GetHeaderValue(headers: searchResponse.Headers, headerName: HttpHeaderName.ETag),
                    LastModified = fhirRequestHttpHeaderSupport.GetLastModified(searchResponse.Headers),
                    Location = fhirBundleCommonSupport.GetHeaderValue(headers: searchResponse.Headers, headerName: HttpHeaderName.Location),
                };
                continue;
            }
            
            if (!endpointPolicyService.GetEndpointPolicy(requestFhirUriResult.Value.ResourceName).AllowRead)
            {
                return operationOutcomeSupport.GetError(new[]
                {
                    $"The entry with the fullUrl of: {getEntry.FullUrl} was unable to be committed as a GET action. " +
                    "(403 Forbidden) The server's endpoint policy controls have refused to authorize this request"
                });
            }
            
            FhirOptionalResourceResponse readResponse = await fhirReadHandler.Handle(
                resourceName: requestFhirUri.ResourceName,
                resourceId: requestFhirUri.ResourceId,
                cancellationToken: cancellationToken,
                headers: GetRequestHeaders(getEntry: getEntry, requestHeaders: requestHeaders));
            
            OperationOutcome? failedReadOperationOutcome = HasReadRequestFailed(readResponse: readResponse, entityCounter: i);
            if (failedReadOperationOutcome is not null)
            {
                return failedReadOperationOutcome;
            }
            
            getEntry.FullUrl = $"{requestFhirUri.PrimaryServiceRootServers}{requestFhirUri.ResourceName}/{requestFhirUri.ResourceId}";
            getEntry.Resource = readResponse.Resource;
            getEntry.Response = new Bundle.ResponseComponent
            {
                Status = readResponse.HttpStatusCode.Display(),
                Etag = fhirBundleCommonSupport.GetHeaderValue(headers: readResponse.Headers, headerName: HttpHeaderName.ETag),
                LastModified = fhirRequestHttpHeaderSupport.GetLastModified(readResponse.Headers),
                Location = fhirBundleCommonSupport.GetHeaderValue(headers: readResponse.Headers, headerName: HttpHeaderName.Location),
            };
        }

        return null;
    }
    private OperationOutcome? IsEntityRequestNull(Bundle.EntryComponent getEntry,
        int entityCounter)
    {
        if (getEntry.Request is null)
        {
            return operationOutcomeSupport.GetError(new[]
            {
                $"Bundle.entry[{(entityCounter + 1).ToString()}].request property was found to be empty. All entries within a Transaction bundle must have a populated request property. "
            });
        }

        return null;
    }
    private Dictionary<string, StringValues> GetRequestHeaders(Bundle.EntryComponent getEntry,
        Dictionary<string, StringValues> requestHeaders)
    {
        var readRequestHeaders = fhirRequestHttpHeaderSupport.GetRequestHeadersFromBundleEntryRequest(getEntry.Request);
        foreach (var requestHeader in requestHeaders)
        {
            if (!readRequestHeaders.ContainsKey(requestHeader.Key))
            {
                readRequestHeaders.Add(requestHeader.Key, requestHeader.Value);
            }
        }
        return readRequestHeaders;
    }
    private OperationOutcome? HasSearchRequestFailed(FhirResourceResponse searchResponse, int entityCounter)
    {
        if (searchResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
        {
            return null;
        }
        
        if (searchResponse.Resource is OperationOutcome operationOutcome)
        {
            return operationOutcomeSupport.GetError(new[]
            {
                $"The Bundle.entry[{(entityCounter + 1).ToString()}].request.url was unable to be processed as a GET action. "
            }, operationOutcome: operationOutcome);
        }

        throw new ApplicationException($"When {nameof(searchResponse.HttpStatusCode)} is {HttpStatusCode.OK.ToString()}, " +
                                       $"the {nameof(searchResponse.Resource)} is expected to be of type OperationOutcome");
    }
    private OperationOutcome? HasReadRequestFailed(FhirOptionalResourceResponse readResponse, int entityCounter)
    {
        if (!readResponse.HttpStatusCode.Equals(HttpStatusCode.BadRequest))
        {
            return null;
        }
        
        if (readResponse.Resource is OperationOutcome operationOutcome)
        {
            return operationOutcomeSupport.GetError(new[]
            {
                $"The Bundle.entry[{(entityCounter + 1).ToString()}].request.url was unable to be processed as a GET action. "
            }, operationOutcome: operationOutcome);
        }

        throw new ApplicationException($"When {nameof(readResponse.HttpStatusCode)} is {HttpStatusCode.BadRequest.ToString()}, " +
                                       $"the {nameof(readResponse.Resource)} is expected to be of type OperationOutcome");
    }
    private bool IsSearchRequest(FhirUri requestFhirUri)
    {
        return string.IsNullOrWhiteSpace(requestFhirUri.ResourceId) && !string.IsNullOrWhiteSpace(requestFhirUri.Query);
    }
    private static Bundle.EntryComponent? GetGetEntry(Bundle.EntryComponent entry)
    {
        if (entry.Request?.Method is not Bundle.HTTPVerb.GET)
        {
            return null;
        }

        return entry;
    }
    
}