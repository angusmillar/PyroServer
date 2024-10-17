using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.FhirQuery;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public abstract class SearchQueryBase(SearchParameterProjection searchParameter, FhirResourceTypeId resourceTypeContext, string rawValue)
{
  protected const char OrDelimiter = ',';
  protected const char CompositeDelimiter = '$';
  protected const char ParameterValueDelimiter = '=';
  public string RawValue { get; set; } = rawValue;
  public SearchModifierCodeId? Modifier { get; set; }
  public FhirResourceTypeId? TypeModifierResource { get; set; }
  public SearchQueryBase? ChainedSearchParameter { get; set; }
  public bool HasLogicalOrProperties { get; set; } = false;
  public bool IsValid { get; set; } = true;
  public string? InvalidMessage { get; set; }
  public ServiceBaseUrl? PrimaryServiceBaseUrl { get; set; }
  public FhirResourceTypeId ResourceTypeContext { get; set; } = resourceTypeContext;

  public SearchParameterProjection SearchParameter { get; set; } = searchParameter;

  public virtual void ParseModifier(string parameterName, IFhirResourceTypeSupport fhirResourceTypeSupport)
  {
    if (parameterName.Contains(FhirQuery.FhirQuery.TermSearchModifierDelimiter))
    {
      string parameterNameModifierPart = parameterName.Split(FhirQuery.FhirQuery.TermSearchModifierDelimiter)[1];
      var searchModifierTypeDic = StringToEnumMap<SearchModifierCodeId>.GetDictionary();
      string valueCaseCorrectly = StringSupport.ToLowerFast(parameterNameModifierPart);
      if (searchModifierTypeDic.TryGetValue(valueCaseCorrectly, out var value))
      {
        Modifier = value;
      }
      else
      {
        string typedResourceName = parameterNameModifierPart;
        if (parameterNameModifierPart.Contains('.'))
        {
          char[] delimiters = { '.' };
          typedResourceName = parameterNameModifierPart.Split(delimiters)[0].Trim();
        }

        FhirResourceTypeId? resourceType = fhirResourceTypeSupport.TryGetResourceType(typedResourceName);
        if (resourceType is not null)
        {
          TypeModifierResource = resourceType.Value;
          Modifier = SearchModifierCodeId.Type;
        }
        else
        {
          throw new ApplicationException($"Unsupported resource type : {typedResourceName}");
        }
      }
    }
    else
    {
      Modifier = null;
      TypeModifierResource = null;
    }

    if (Modifier.HasValue)
    {
      SearchModifierCodeId[] oSupportedModifierArray = FhirSearchQuerySupport.GetModifiersForSearchType(SearchParameter.Type);
      if (oSupportedModifierArray.All(x => x != Modifier.Value))
      {
        InvalidMessage += $"The parameter's modifier: '{this.Modifier.GetCode()}' is not supported by this server for this search parameter type '{SearchParameter.Type.GetCode()}', the whole parameter was : '{RawValue}', ";
        IsValid = false;
      }

    }
  }
  public abstract Task ParseValue(string value);
  public abstract object CloneDeep();
  public virtual object CloneDeep(SearchQueryBase clone)
  {
    clone.SearchParameter = clone.SearchParameter;
    clone.RawValue = RawValue;
    clone.Modifier = Modifier;
    clone.TypeModifierResource = TypeModifierResource;
    clone.ChainedSearchParameter = ChainedSearchParameter;
    clone.HasLogicalOrProperties = HasLogicalOrProperties;
    clone.IsValid = IsValid;
    clone.InvalidMessage = InvalidMessage;
    clone.PrimaryServiceBaseUrl = PrimaryServiceBaseUrl;

    return clone;
  }


}
