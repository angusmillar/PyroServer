using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirQuery;
using Abm.Pyro.Domain.SearchQueryEntity;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.FhirSupport;

namespace Abm.Pyro.Domain.SearchQuery
{
  public class SearchQueryServiceOutcome(
    FhirResourceTypeId resourceContext, 
    IFhirQuery fhirQuery) 
    : IValidatable 
  {
    public bool HasInvalidQuery
    {
      get
      {
        return (InvalidSearchQueryList.Count > 0);
      }
    }
    public bool HasUnsupportedQuery
    {
      get
      {
        return (UnsupportedSearchQueryList.Count > 0);
      }
    }
    public FhirResourceTypeId ResourceContext { get; set; } = resourceContext;
    public int? CountRequested { get; set; } = fhirQuery.Count;
    public int? PageRequested { get; set; } = fhirQuery.Page;
    public string? Content { get; set; } = fhirQuery.Content;
    public string? Text { get; set; } = fhirQuery.Text;
    public string? Filter { get; set; } = fhirQuery.Filter;

    public Dictionary<string, StringValues> OriginalSearchQuery { get; set; } = new();
    public string? Query { get; set; } = fhirQuery.Query;
    public ContainedSearch? Contained { get; set; } = fhirQuery.Contained;
    public ContainedType? ContainedType { get; set; } = fhirQuery.ContainedType;
    public SummaryType? SummaryType { get; set; } = fhirQuery.SummaryType;
    public IList<SearchQueryHas> HasList { get; set; } = new List<SearchQueryHas>();
    public IList<SearchQueryBase> SearchQueryList { get; set; } = new List<SearchQueryBase>();
    public IList<SearchQueryInclude> IncludeList { get; set; } = new List<SearchQueryInclude>();
    public IList<InvalidQueryParameter> InvalidSearchQueryList { get; set; } = fhirQuery.InvalidParameterList;
    public IList<InvalidQueryParameter> UnsupportedSearchQueryList { get; set; } = new List<InvalidQueryParameter>();

    public IEnumerable<string> InvalidSearchQueryMessageList()
    {
      var messageList = new List<string>();
      foreach (var invalid in InvalidSearchQueryList)
      {
        messageList.Add($"Query parameter: {invalid.Name}={invalid.Value} was invalid for the following reason: {invalid.ErrorMessage} ");
      }

      return messageList;
    }
    
    public IEnumerable<string> UnsupportedQueryMessageList()
    {
      var messageList = new List<string>();
      foreach (var Invalid in UnsupportedSearchQueryList)
      {
        messageList.Add($"Unsupported query parameter: {Invalid.Name}={Invalid.Value} was unsupported for the following reason: {Invalid.ErrorMessage} ");
      }

      return messageList;
    }
    
    public IEnumerable<string> InvalidAndUnsupportedQueryMessageList()
    {
      var messageList = new List<string>(InvalidSearchQueryMessageList());
      messageList.AddRange(UnsupportedQueryMessageList());
      
      return messageList;
    }
    
    // public OperationOutcome InvalidQueryOperationOutCome(IOperationOutcomeSupport operationOutcomeSupport)
    // {
    //   var messageList = new List<string>();
    //   foreach (var invalid in InvalidSearchQueryList)
    //   {
    //     messageList.Add($"Query parameter: {invalid.Name}={invalid.Value} was invalid for the following reason: {invalid.ErrorMessage} ");
    //   }
    //   return operationOutcomeSupport.GetError(messageList.ToArray());
    // }
    // public Resource InvalidAndUnsupportedQueryOperationOutCome(IOperationOutcomeSupport operationOutcomeSupport)
    // {
    //   var messageList = new List<string>();
    //   foreach(var invalid in InvalidSearchQueryList)
    //   {        
    //     messageList.Add($"Invalid query parameter: {invalid.Name}={invalid.Value} was invalid for the following reason: {invalid.ErrorMessage} ");
    //   }
    //   foreach (var Invalid in UnsupportedSearchQueryList)
    //   {
    //     messageList.Add($"Unsupported query parameter: {Invalid.Name}={Invalid.Value} was unsupported for the following reason: {Invalid.ErrorMessage} ");
    //   }
    //   return operationOutcomeSupport.GetError(messageList.ToArray());
    // }
    
  }
}
