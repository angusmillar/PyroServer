using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.SearchQueryEntity;

  public class SearchQueryToken(SearchParameterProjection searchParameter, FhirResourceTypeId resourceTypeContext, string rawValue) : SearchQueryBase(searchParameter, resourceTypeContext, rawValue)
  {
    protected const char TokenDelimiter = '|';

    public List<SearchQueryTokenValue> ValueList { get; set; } = new();

    public override object CloneDeep()
    {
      var clone = new SearchQueryToken(SearchParameter, ResourceTypeContext, RawValue);
      base.CloneDeep(clone);
      clone.ValueList = new List<SearchQueryTokenValue>();
      clone.ValueList.AddRange(ValueList);
      return clone;
    }

    public override Task ParseValue(string values)
    {
      IsValid = true;
      ValueList = new List<SearchQueryTokenValue>();
      foreach (var value in values.Split(OrDelimiter))
      {
        if (Modifier is SearchModifierCodeId.Missing)
        {
          bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
          if (isMissing.HasValue)
          {
            ValueList.Add(new SearchQueryTokenValue(isMissing.Value, null, null, null));
          }
          else
          {
            InvalidMessage = $"Found the {SearchModifierCodeId.Missing.GetCode()} Modifier yet is value was expected to be true or false yet found '{value}'. ";
            IsValid = false;
            break;
          }
        }
        else
        {
          if (value.Contains(TokenDelimiter))
          {
            string[] codeSystemSplit = value.Split(TokenDelimiter);
            string code = codeSystemSplit[1].Trim();
            string system = codeSystemSplit[0].Trim();
            SearchQueryTokenValue.TokenSearchType? tokenSearchType;
            if (String.IsNullOrEmpty(code) && String.IsNullOrEmpty(system))
            {
              InvalidMessage = $"Unable to parse the Token search parameter value of '{value}' as there was no Code and System separated by a '{TokenDelimiter}' delimiter";
              IsValid = false;
              break;
            }
            if (String.IsNullOrEmpty(system))
            {
              tokenSearchType = SearchQueryTokenValue.TokenSearchType.MatchCodeWithNullSystem;
            }
            else if (String.IsNullOrEmpty(code))
            {
              tokenSearchType = SearchQueryTokenValue.TokenSearchType.MatchSystemOnly;
            }
            else
            {
              tokenSearchType = SearchQueryTokenValue.TokenSearchType.MatchCodeAndSystem;
            }

            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(system))
            {
              ValueList.Add(new SearchQueryTokenValue(false, tokenSearchType, code, system));
            }
            else if (!string.IsNullOrWhiteSpace(system))
            {
              ValueList.Add(new SearchQueryTokenValue(false, tokenSearchType, null, system));
            }
            else if (!string.IsNullOrWhiteSpace(code))
            {
              ValueList.Add(new SearchQueryTokenValue(false, tokenSearchType, code, null));
            }
          }
          else
          {
            SearchQueryTokenValue.TokenSearchType? tokenSearchType = SearchQueryTokenValue.TokenSearchType.MatchCodeOnly;
            string code = value.Trim();
            if (String.IsNullOrEmpty(code))
            {
              InvalidMessage = $"Unable to parse the Token search parameter value of '{value}' as there was no Code found.";
              IsValid = false;
              break;
            }
            ValueList.Add(new SearchQueryTokenValue(false, tokenSearchType, code, null));
          }
        }
      }

      if (ValueList.Count > 1)
      {
        HasLogicalOrProperties = true;
      }

      if (ValueList.Count != 0)
      {
        return Task.CompletedTask;;
      }
      
      InvalidMessage = $"Unable to parse any values into a {GetType().Name} from the string: {values}.";
      IsValid = false;
      
      return Task.CompletedTask;
    }
    
  }

