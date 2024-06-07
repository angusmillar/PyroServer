using System.Data;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Support;
using Microsoft.Extensions.Primitives;
using Hl7.Fhir.Rest;

namespace Abm.Pyro.Domain.FhirQuery;

public class FhirQuery : IFhirQuery
{
  public const string TermInclude = "_include";
  public const string TermRevInclude = "_revinclude";
  public const string TermIncludeIterate = "iterate";
  public const string TermIncludeRecurse = "recurse";
  public const string TermSort = "_sort";
  public const string TermCount = "_count";
  public const string TermContained = "_contained";
  public const string TermContainedType = "_containedType";
  public const string TermSummary = "_summary";
  public const string TermText = "_text";
  public const string TermContent = "_content";
  public const string TermQuery = "_query";
  public const string TermFilter = "_filter";
  public const string TermPage = "page";
  public const char TermChainDelimiter = '.';
  public const char TermSearchModifierDelimiter = ':';
  public const string TermHas = "_has";
  
  public int? Count { get; set; }
  public int? Page { get; set; }
  public IList<IncludeParameter> RevInclude { get; set; } = new List<IncludeParameter>();
  public IList<IncludeParameter> Include { get; set; } = new List<IncludeParameter>();
  public IList<SortParameter> Sort { get; set; } = new List<SortParameter>();
  public IList<HasParameter> Has { get; set; } = new List<HasParameter>();
  public IList<InvalidQueryParameter> InvalidParameterList { get; set; } = new List<InvalidQueryParameter>();
  public Enums.ContainedSearch? Contained { get; set; }
  public ContainedType? ContainedType { get; set; }
  public Enums.SummaryType? SummaryType { get; set; }
  public Dictionary<string, StringValues> ParametersDictionary { get; set; } = new();
  public string? Content { get; set; }
  public string? Text { get; set; }
  public string? Query { get; set; }
  public string? Filter { get; set; }
  private bool QueryItemProcessed { get; set; } = false;

  public bool Parse(Dictionary<string, StringValues> query)
  {
    InvalidParameterList = new List<InvalidQueryParameter>();
    RevInclude = new List<IncludeParameter>();
    Include = new List<IncludeParameter>();
    Sort = new List<SortParameter>();
    ParametersDictionary = new Dictionary<string, StringValues>();
    QueryItemProcessed = false;

    foreach (var item in query)
    {
      QueryItemProcessed = false;
      //Count
      if (TryParseIntegerParameter(item, TermCount, out int countValue))
      {
        Count = countValue;
      }
      
      //Page
      if (TryParseIntegerParameter(item, TermPage, out int pageValue))
      {
        Page = pageValue;
      }
      
      //Include & RevInclude
      ParseIncludes(item);

      //Sort
      ParseSortParameter(item);

      //Has
      ParseHasParameter(item);

      //Contained      
      if (TryParseSingleEnumTerm(item, TermContained, out Enums.ContainedSearch contained))
      {
        Contained = contained;
      }
      //ContainedType       
      if (TryParseSingleEnumTerm(item, TermContainedType, out ContainedType containedType))
      {
        ContainedType = containedType;
      }

      //SummaryType       
      if (TryParseSingleEnumTerm(item, TermContained, out Enums.SummaryType summaryType))
      {
        SummaryType = summaryType;
      }

      //Content
      Content = GetSimpleStringParameter(item, FhirQuery.TermContent);

      //Text
      Text = GetSimpleStringParameter(item, FhirQuery.TermText);

      //Query
      Query = GetSimpleStringParameter(item, FhirQuery.TermQuery);

      //Filter
      Filter = GetSimpleStringParameter(item, FhirQuery.TermFilter);

      //And any other query parameters to the Parameters list, these will be the general resource search parameters.
      if (!QueryItemProcessed)
      {
        ParametersDictionary.Add(item.Key, item.Value);
      }

    }
    return (!InvalidParameterList.Any());
  }

  private string? GetSimpleStringParameter(KeyValuePair<string, StringValues> item, string term)
  {
    if (item.Key == term)
    {
      QueryItemProcessed = true;
      if (!IsParameterValueEmpty(item))
      {
        CheckSingleParameterForMoreThanOne(item);
        return item.Value[item.Value.Count - 1];
      }
    }
    return null;
  }

  private void CheckSingleParameterForMoreThanOne(KeyValuePair<string, StringValues> item)
  {
    if (item.Value.Count > 1)
    {
      for (int i = 0; i < item.Value.Count - 1; i++)
      {
        
        InvalidParameterList.Add(new InvalidQueryParameter(item.Key, item.Value[i] ?? string.Empty, $"Found many parameters of the same type where only one can be provided, only the last instance will be used."));
      }
    }
  }

  private bool IsParameterValueEmpty(KeyValuePair<string, StringValues> item)
  {
    if (item.Value.Count == 0)
    {
      InvalidParameterList.Add(new InvalidQueryParameter(item.Key, string.Empty, $"The parameter did not contain a value."));
      return true;
    }
    return false;
  }

  private bool TryParseSingleEnumTerm<TEnumType>(KeyValuePair<string, StringValues> item, string term, out TEnumType? enumValue)
    where TEnumType : Enum
  {
    if (item.Key == term)
    {
      QueryItemProcessed = true;
      if (TryParseSingleEnum<TEnumType>(item, out TEnumType? parsedEnumValue))
      {
        enumValue = parsedEnumValue;
        return true;
      }
    }
    enumValue = default;
    return false;
  }

  private bool TryParseSingleEnum<TEnumType>(KeyValuePair<string, StringValues> item, out TEnumType? enumValue)
    where TEnumType : Enum
  {
    if (!IsParameterValueEmpty(item))
    {
      CheckSingleParameterForMoreThanOne(item);
      string value = item.Value[item.Value.Count - 1] ?? string.Empty;
      string valueLower = StringSupport.ToLowerFast(value);
      Dictionary<string, TEnumType> dic = StringToEnumMap<TEnumType>.GetDictionary();
      if (dic.TryGetValue(valueLower, out var tempEnumValue))
      {
        enumValue = tempEnumValue;
        return true;
      }
      InvalidParameterList.Add(new InvalidQueryParameter(item.Key, value, $"Unable to parse the provided value to an allowed value."));
    }
    enumValue = default;
    return false;
  }

  private void ParseHasParameter(KeyValuePair<string, StringValues> item)
  {
    //GET [base]/Patient?_has:Observation:patient:code=1234-5
    //or
    //GET [base]/Patient?_has:Observation:patient:_has:AuditEvent:entity:user=MyUserId
    if (item.Key.StartsWith($"{TermHas}{TermSearchModifierDelimiter}"))
    {
      QueryItemProcessed = true;
      var hasSplit = item.Key.Split(TermHas);
      HasParameter? rootHasParameter = null;
      HasParameter? previousHasParameter = null;
      for (int i = 1; i < hasSplit.Length; i++)
      {
        var modifierSplit = hasSplit[i].Split(FhirQuery.TermSearchModifierDelimiter);
        if (modifierSplit.Length == 4 && rootHasParameter is null)
        {
          if (modifierSplit[3] == string.Empty)
          {
            rootHasParameter = new HasParameter(modifierSplit[1], modifierSplit[2]);
            rootHasParameter.RawHasParameter = $"{item.Key}={item.Value}";
            previousHasParameter = rootHasParameter;
          }
          else
          {
            rootHasParameter = new HasParameter(modifierSplit[1], modifierSplit[2]);
            rootHasParameter.SearchQuery = new KeyValuePair<string, StringValues>(modifierSplit[3], item.Value);
            rootHasParameter.RawHasParameter = $"{item.Key}={item.Value}";
            previousHasParameter = rootHasParameter;
          }
        }
        else if (modifierSplit.Length == 4 && rootHasParameter is not null)
        {
          if (modifierSplit[3] == string.Empty)
          {
            previousHasParameter!.ChildHasParameter = new HasParameter(modifierSplit[1], modifierSplit[2]);
            previousHasParameter = previousHasParameter!.ChildHasParameter;
          }
          else
          {
            previousHasParameter!.ChildHasParameter = new HasParameter(modifierSplit[1], modifierSplit[2]);
            previousHasParameter.ChildHasParameter.SearchQuery = new KeyValuePair<string, StringValues>(modifierSplit[3], item.Value);
            previousHasParameter = previousHasParameter!.ChildHasParameter;
          }
        }
        else
        {
          InvalidParameterList.Add(new InvalidQueryParameter(item.Key, item.Value.ToString(), $"The {TermHas} query must contain a resource name followed by a reference search parameter name followed by another {TermHas} parameter or a search parameter and value where each is separated by a colon {TermSearchModifierDelimiter}. For instance: _has:Observation:patient:code=1234-5 or _has:Observation:patient:_has:AuditEvent:entity:user=MyUserId. The {TermHas} qery found was : {item.Key}={item.Value} "));
        }
      }

      if (rootHasParameter is not null)
      {
        Has.Add(rootHasParameter);
      }
    }
  }

  private void ParseSortParameter(KeyValuePair<string, StringValues> item)
  {
    if (item.Key == TermSort)
    {
      QueryItemProcessed = true;
      foreach (var sortValue in item.Value)
      {
        if (sortValue is not null)
        {
          if (sortValue.StartsWith('-'))
          {
            Sort.Add(new SortParameter(SortOrder.Descending, sortValue.TrimStart('-')));
          }
          else
          {
            Sort.Add(new SortParameter(SortOrder.Descending, sortValue));
          }
        }
      }
    }
  }
  
  private void ParseIncludes(KeyValuePair<string, StringValues> item)
  {
    string[] itemArray = item.Key.Split(TermSearchModifierDelimiter);
    IncludeType? includeType = TryGetIncludeType(itemArray.First());
    if (includeType is null)
    {
      return;
    }
    
    if (itemArray.Length > 2)
    {
      string errorMessage = $"Found to many '{TermSearchModifierDelimiter}' characters with in the {includeType.GetCode()} search parameter's token key. " +
                            $"This server only supports a count of one or none, for instance (e.g. {includeType.GetCode()}{TermSearchModifierDelimiter}{TermIncludeIterate} or just {includeType.GetCode()})";
      InvalidParameterList.Add(new InvalidQueryParameter(name: item.Key, value: item.Value.ToString(), errorMessage: errorMessage));
      return;
    }
    
    bool iterate = false;
    bool recurse = false;
    
    if (itemArray.Length == 2)
    {
      recurse = IsTermIncludeRecurse(itemArray[1]);
      iterate = IsTermIncludeIterate(itemArray[1]);
      if (!(recurse || iterate))
      {
        string errorMessage = $"Found an unexpected token of '{itemArray[1]}' after the {includeType.GetCode()} parameter type. " +
                              $"This server only supports {TermIncludeIterate} or {TermIncludeRecurse})";
        InvalidParameterList.Add(new InvalidQueryParameter(name: item.Key, value: item.Value.ToString(), errorMessage: errorMessage));
        return;
      }
    }

    foreach (string? value in item.Value)
    {
      Include.Add(new IncludeParameter(iterate, recurse, includeType.Value, value ?? string.Empty));  
    }
    
    QueryItemProcessed = true;
  }

  private bool IsTermIncludeIterate(string secondItem)
  {
    return secondItem.Equals(TermIncludeIterate);
  }

  private bool IsTermIncludeRecurse(string secondItem)
  {
    return secondItem.Equals(TermIncludeRecurse);
  }

  private IncludeType? TryGetIncludeType(string firstItem)
  {
    if (firstItem.Equals(TermInclude, StringComparison.Ordinal))
    {
      return IncludeType.Include;
    }
    
    if (firstItem.Equals(TermRevInclude, StringComparison.Ordinal))
    {
      return  IncludeType.Revinclude;
    }

    return null;
  }


  private bool TryParseIntegerParameter(KeyValuePair<string, StringValues> item, string term, out int integerValue)
  {
    integerValue = 0;
    if (!item.Key.Equals(term))
    {
      return false;
    }

    QueryItemProcessed = true;
    string? firstValue = item.Value.FirstOrDefault();
    if (firstValue is not null && int.TryParse(firstValue.Trim(), out int countInt))
    {
      integerValue = countInt;
      return true;
    }
    InvalidParameterList.Add(new InvalidQueryParameter(item.Key, item.Value.ToString(), $"Unable to parse the value to an integer."));
    return false;
  }

  public class SortParameter(SortOrder sortOrder, string searchParameter)
  {
    public SortOrder SortOrder { get; set; } = sortOrder;
    public string SearchParameter { get; set; } = searchParameter;
  }

  public class IncludeParameter(bool iterate, bool recurse, IncludeType type, string value)
  {
    public IncludeType Type { get; set; } = type;
    public bool Recurse { get; set; } = recurse;
    public bool Iterate { get; set; } = iterate;
    public string Value { get; set; } = value;
  }

  public class HasParameter(string targetResource, string referenceSearchParameterName)
  {
    public string RawHasParameter { get; set; } = String.Empty;
    public HasParameter? ChildHasParameter { get; set; }
    public string TargetResourceForSearchQuery { get; set; } = targetResource;
    public string BackReferenceSearchParameterName { get; set; } = referenceSearchParameterName;
    public KeyValuePair<string, StringValues>? SearchQuery { get; set; }
  }

}
