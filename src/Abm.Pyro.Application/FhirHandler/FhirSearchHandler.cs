using System.Net;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Validation;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.FhirHandler;

public class FhirSearchHandler(
  IValidator validator,
  IFhirResourceTypeSupport fhirResourceTypeSupport,
  ISearchQueryService searchQueryService,
  IResourceStoreSearch resourceStoreSearch,
  IFhirBundleCreationSupport fhirBundleCreationSupport,
  IPaginationSupport paginationSupport,
  IFhirResponseHttpHeaderSupport fhirResponseHttpHeaderSupport)
  : IRequestHandler<FhirSearchRequest, FhirResourceResponse>, IFhirSearchHandler
{
  public async Task<FhirResourceResponse> Handle(string resourceName, string query, Dictionary<string, StringValues> headers, CancellationToken cancellationToken)
  {
    return await Handle(new FhirSearchRequest(
      RequestSchema: "http",
      RequestPath: string.Empty,
      QueryString: query,
      Headers: headers,
      ResourceName: resourceName,
      TimeStamp: DateTimeOffset.Now), cancellationToken: cancellationToken);
  }
  
  public async Task<FhirResourceResponse> Handle(FhirSearchRequest request, CancellationToken cancellationToken)
  {
    ValidatorResult requestValidatorResult = validator.Validate(request);
    if (!requestValidatorResult.IsValid)
    {
      return InvalidValidatorResultResponse(requestValidatorResult);
    }
    
    FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType(request.ResourceName);
    
    SearchQueryServiceOutcome searchQueryServiceOutcome = await searchQueryService.Process(fhirResourceType, request.QueryString);
    ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
      searchQueryServiceOutcome: searchQueryServiceOutcome, 
      headers: request.Headers));
    if (!searchQueryValidatorResult.IsValid)
    {
      return InvalidValidatorResultResponse(searchQueryValidatorResult);
    }
   
    ResourceStoreSearchOutcome resourceStoreSearchOutcome = await resourceStoreSearch.GetSearch(searchQueryServiceOutcome);

    Bundle bundle = await fhirBundleCreationSupport.CreateBundle(resourceStoreSearchOutcome, Bundle.BundleType.Searchset, request.RequestSchema);
    
    await paginationSupport.SetBundlePagination(bundle: bundle, 
                                          searchQueryServiceOutcome: searchQueryServiceOutcome, 
                                          requestSchema: request.RequestSchema,
                                          requestPath: request.RequestPath,
                                          pagesTotal: resourceStoreSearchOutcome.PagesTotal, 
                                          pageCurrentlyRequired:resourceStoreSearchOutcome.PageRequested);
    
    var headers = fhirResponseHttpHeaderSupport.ForSearch(requestTimeStamp: request.TimeStamp);
    
    return new FhirResourceResponse(
      Resource: bundle,
      HttpStatusCode: HttpStatusCode.OK,
      Headers: headers,
      ResourceOutcomeInfo: null);
  }
  
  private static FhirResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
  {
    return new FhirResourceResponse(
      Resource: validatorResult.GetOperationOutcome(), 
      HttpStatusCode: validatorResult.GetHttpStatusCode(),
      Headers: new Dictionary<string, StringValues>());
  }
}
