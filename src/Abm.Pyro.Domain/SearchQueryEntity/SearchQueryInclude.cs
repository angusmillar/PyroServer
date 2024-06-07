using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;
using Hl7.Fhir.Utility;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryInclude(IncludeType includeType)
{
  public static string IterateName
  {
    get
    {
      return "iterate";
    }
  }
  public IncludeType Type { get; } = includeType;
  public FhirResourceTypeId? SourceResourceType { get; set; }
  public List<SearchParameterProjection> SearchParameterList { get; set; } = new();
  public FhirResourceTypeId? SearchParameterTargetResourceType { get; set; }
  public bool IsRecurse { get; set; } = false;
  public bool IsIterate { get; set; } = false;

  public bool IsRecurseIterate
  {
    get
    {
      return (IsIterate || IsRecurse);
    }
  }
  
  public string AsFormattedSearchParameter()
  {
    //example:
    //_include:recurse=SourceResourceType:SearchParameterTargetResourceType      
    //_revinclude:recurse=SourceResourceType:SearchParameterTargetResourceType

    string result;
    if (Type == IncludeType.Include)
    {
      result = Hl7.Fhir.Rest.SearchParams.SEARCH_PARAM_INCLUDE;
    }
    else
    {
      result = Hl7.Fhir.Rest.SearchParams.SEARCH_PARAM_REVINCLUDE;
    }

    if (IsRecurse)
    {
      result += $":{IterateName}";
    }

    if (SourceResourceType.HasValue)
    {
      result += $"={SourceResourceType.GetCode()}";  
    }
    
    if (SearchParameterList.Count == 1)
    {
      result += $":{SearchParameterList[0].Code}";
    }
    if (SearchParameterList.Count > 1)
    {
      result += $":*";
    }
    if (SearchParameterTargetResourceType.HasValue)
    {
      result += $":{SearchParameterTargetResourceType.GetLiteral()}";
    }
    return result;
  }
}
