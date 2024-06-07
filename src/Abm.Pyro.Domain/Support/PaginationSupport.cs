using System.Collections.Specialized;
using System.Web;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.SearchQueryEntity;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.Support;

public class PaginationSupport(
    IOptions<PaginationSettings> paginationSettings,
    IServiceBaseUrlCache serviceBaseUrlCache) : IPaginationSupport
{
    public int CalculatePageRequired(int? requiredPageNumber,
        int? countOfRecordsRequested,
        int totalRecordCount)
    {
        int numberOfRecordsPerPage = SetNumberOfRecordsPerPage(countOfRecordsRequested);

        if (totalRecordCount == 0 || !requiredPageNumber.HasValue || requiredPageNumber < 1)
        {
            return 1;
        }
        
        int totalPages = CalculateTotalPages(numberOfRecordsPerPage, totalRecordCount);
        if (requiredPageNumber >= totalPages)
        {
            return totalPages;
        }

        return requiredPageNumber.Value;
    }

    public int CalculateTotalPages(int? countOfRecordsRequested,
        int totalRecordCount)
    {
        int numberOfRecordsPerPage = SetNumberOfRecordsPerPage(countOfRecordsRequested);
        if (totalRecordCount == 0 || numberOfRecordsPerPage == 0)
        {
            return 1;
        }
        
        int totalPages = (totalRecordCount / numberOfRecordsPerPage);
        if ((totalRecordCount % numberOfRecordsPerPage) == 0)
        {
            return totalPages;
        }

        return totalPages + 1;
    }

    public int SetNumberOfRecordsPerPage(int? countOfRecordsRequested)
    {
        if (countOfRecordsRequested.HasValue)
        {
            if (countOfRecordsRequested.Value <= paginationSettings.Value.MaximumNumberOfRecordsPerPage)
            {
                return countOfRecordsRequested.Value;
            }

            return paginationSettings.Value.MaximumNumberOfRecordsPerPage;
        }

        return paginationSettings.Value.DefaultNumberOfRecordsPerPage;
    }

    public async System.Threading.Tasks.Task SetBundlePagination(Bundle bundle,
        SearchQueryServiceOutcome searchQueryServiceOutcome,
        string requestSchema,
        string requestPath,
        int pagesTotal,
        int pageCurrentlyRequired)
    {
        ServiceBaseUrl primaryServiceBaseUrl = await serviceBaseUrlCache.GetRequiredPrimaryAsync();
        Uri requestBase = new Uri($"{requestSchema}://{primaryServiceBaseUrl.Url}");
        Uri requestUri = new Uri(requestBase, requestPath);
        
        bundle.SelfLink = GetSelfLink(searchQueryServiceOutcome, requestUri);
 
        //I don't know why but if you assign null to these bundle links you get an exception, even though they appear as nullable types.
        //So the following assignments MUST be wrapped in null checks as seen below (bundle.FirstLink, bundle.PreviousLink, bundle.NextLink, bundle.LastLink)
        Uri? firstLink = GetPageNavigationUri(requestUri, searchQueryServiceOutcome.OriginalSearchQuery, GetFirstPageNumber());
        if (firstLink is not null)
        {
            bundle.FirstLink = firstLink;
        }
        
        Uri? previousLink = GetPageNavigationUri(requestUri, searchQueryServiceOutcome.OriginalSearchQuery, GetPreviousPageNumber(pageCurrentlyRequired, pagesTotal));
        if (previousLink is not null)
        {
            bundle.PreviousLink = previousLink;
        }
        
        Uri? nextLink = GetPageNavigationUri(requestUri, searchQueryServiceOutcome.OriginalSearchQuery, GetNextPageNumber(pageCurrentlyRequired, pagesTotal));
        if (nextLink is not null)
        {
            bundle.NextLink = nextLink;
        }
        
        Uri? lastLink = GetPageNavigationUri(requestUri, searchQueryServiceOutcome.OriginalSearchQuery, GetLastPageNumber(pagesTotal));
        if (lastLink is not null)
        {
            bundle.LastLink = lastLink;
        }
    }

    private int GetFirstPageNumber()
    {
        return 1;
    }

    private int GetLastPageNumber(int pagesTotal)
    {
        return pagesTotal;
    }

    private int? GetNextPageNumber(int pageCurrentlyRequired,
        int pagesTotal)
    {
        if (pageCurrentlyRequired < 1)
            pageCurrentlyRequired = 1;
        if (pageCurrentlyRequired >= pagesTotal)
        {
            return null;
        }

        return pageCurrentlyRequired + 1;
    }

    private int? GetPreviousPageNumber(int pageCurrentlyRequired,
        int pagesTotal)
    {
        if (pageCurrentlyRequired <= 1)
        {
            return null;
        }

        if (pageCurrentlyRequired >= pagesTotal)
        {
            return pagesTotal - 1;
        }

        return pageCurrentlyRequired - 1;
    }


    private Uri? GetPageNavigationUri(Uri requestUri, Dictionary<string, StringValues> query,
        int? newPageNumber)
    {
        //If the page number is null then we don't need a link as we are currently at the end or the beginning
        if (!newPageNumber.HasValue)
            return null;
        
        const string pageToken = "page";

        query[pageToken] = newPageNumber.ToString();
        
        UriBuilder navigationUri = new UriBuilder(requestUri);
        navigationUri.Query = query.ToEncodedQueryString();
        return navigationUri.Uri;
    }
    
    private static (string name, string value) SplitNameValue(string nameValue)
    {
        const char equalsToken = '=';
        string[] split = nameValue.Split(equalsToken);
        return new(split[0], split[1]);
    }

    private Uri GetSelfLink(SearchQueryServiceOutcome searchQueryServiceOutcome,
        Uri requestUri)
    {
        NameValueCollection queryNameValueCollection = HttpUtility.ParseQueryString(requestUri.Query);
        queryNameValueCollection.Clear(); //https://stackoverflow.com/questions/3865975/namevaluecollection-to-url-query

        GetStandardSearchParameters(searchQueryServiceOutcome, queryNameValueCollection);

        GetIncludeSearchParameters(searchQueryServiceOutcome, queryNameValueCollection);

        GetSummarySearchParameter(searchQueryServiceOutcome, queryNameValueCollection);

        GetCountSearchParameter(searchQueryServiceOutcome, queryNameValueCollection);

        GetHasSearchParameter(searchQueryServiceOutcome, queryNameValueCollection);

        return new UriBuilder(requestUri) { Query = queryNameValueCollection.ToString()}.Uri;
    }

    private static void GetHasSearchParameter(SearchQueryServiceOutcome searchQueryServiceOutcome,
        NameValueCollection queryNameValueCollection)
    {
        if (searchQueryServiceOutcome.PageRequested.HasValue)
        {
            queryNameValueCollection.Add("page", searchQueryServiceOutcome.PageRequested.Value.ToString());
        }
    }

    private void GetCountSearchParameter(SearchQueryServiceOutcome searchQueryServiceOutcome,
        NameValueCollection queryNameValueCollection)
    {
        if (searchQueryServiceOutcome.CountRequested.HasValue)
        {
            queryNameValueCollection.Add("_count", SetNumberOfRecordsPerPage(searchQueryServiceOutcome.CountRequested).ToString());
        }
    }

    private static void GetSummarySearchParameter(SearchQueryServiceOutcome searchQueryServiceOutcome,
        NameValueCollection queryNameValueCollection)
    {
        if (searchQueryServiceOutcome.SummaryType.HasValue)
        {
            queryNameValueCollection.Add("_summary", searchQueryServiceOutcome.SummaryType.Value.GetLiteral());
        }
    }

    private static void GetIncludeSearchParameters(SearchQueryServiceOutcome searchQueryServiceOutcome,
        NameValueCollection queryNameValueCollection)
    {
        foreach (var searchQueryInclude in searchQueryServiceOutcome.IncludeList)
        {
            var nameValue = SplitNameValue(searchQueryInclude.AsFormattedSearchParameter());
            queryNameValueCollection.Add(nameValue.name, nameValue.value);
        }
    }

    private void GetStandardSearchParameters(SearchQueryServiceOutcome searchQueryServiceOutcome,
        NameValueCollection queryNameValueCollection)
    {
        foreach (SearchQueryBase searchQueryBase in searchQueryServiceOutcome.SearchQueryList)
        {
            GetStandardSearchParameter(searchQueryBase, queryNameValueCollection);
        }
    }

    private void GetStandardSearchParameter(SearchQueryBase searchQueryBase,
        NameValueCollection queryNameValueCollection)
    {
        if (searchQueryBase is SearchQueryReference searchParameterReference && searchParameterReference.IsChained)
        {
            string queryItemNameValue = ResolveChainParameterString(searchParameterReference, String.Empty);
            var nameValue = SplitNameValue(queryItemNameValue);
            queryNameValueCollection.Add(nameValue.name, nameValue.value);
        }
        else
        {
            queryNameValueCollection.Add(searchQueryBase.RawValue.Split('=')[0], searchQueryBase.RawValue.Split('=')[1]);
        }
    }

    private string ResolveChainParameterString(SearchQueryBase searchQueryBase,
        string parameterString)
    {
        if (searchQueryBase.ChainedSearchParameter != null)
        {
            parameterString += searchQueryBase.RawValue;
            parameterString = ResolveChainParameterString(searchQueryBase.ChainedSearchParameter, parameterString);
            return parameterString;
        }

        parameterString += searchQueryBase.RawValue;
        return parameterString;
    }
}