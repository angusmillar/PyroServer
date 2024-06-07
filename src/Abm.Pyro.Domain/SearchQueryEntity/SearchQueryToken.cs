using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.SearchQueryEntity;

  public class SearchQueryToken(SearchParameterProjection searchParameter, FhirResourceTypeId resourceTypeContext, string rawValue) : SearchQueryBase(searchParameter, resourceTypeContext, rawValue)
  {
    protected const char TokenDelimiter = '|';

    public List<SearchQueryTokenValue> ValueList { get; set; } = new();

    public override object CloneDeep()
    {
      var clone = new SearchQueryToken(SearchParameter, this.ResourceTypeContext, this.RawValue);
      base.CloneDeep(clone);
      clone.ValueList = new List<SearchQueryTokenValue>();
      clone.ValueList.AddRange(this.ValueList);
      return clone;
    }

    public override void ParseValue(string values)
    {
      this.IsValid = true;
      ValueList = new List<SearchQueryTokenValue>();
      foreach (var value in values.Split(OrDelimiter))
      {
        if (this.Modifier is SearchModifierCodeId.Missing)
        {
          bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
          if (isMissing.HasValue)
          {
            ValueList.Add(new SearchQueryTokenValue(isMissing.Value, null, null, null));
          }
          else
          {
            this.InvalidMessage = $"Found the {SearchModifierCodeId.Missing.GetCode()} Modifier yet is value was expected to be true or false yet found '{value}'. ";
            this.IsValid = false;
            break;
          }
        }
        else
        {
          if (value.Contains(TokenDelimiter))
          {
            string[] codeSystemSplit = value.Split(SearchQueryToken.TokenDelimiter);
            string code = codeSystemSplit[1].Trim();
            string system = codeSystemSplit[0].Trim();
            SearchQueryTokenValue.TokenSearchType? tokenSearchType;
            if (String.IsNullOrEmpty(code) && String.IsNullOrEmpty(system))
            {
              this.InvalidMessage = $"Unable to parse the Token search parameter value of '{value}' as there was no Code and System separated by a '{SearchQueryToken.TokenDelimiter}' delimiter";
              this.IsValid = false;
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
              this.InvalidMessage = $"Unable to parse the Token search parameter value of '{value}' as there was no Code found.";
              this.IsValid = false;
              break;
            }
            ValueList.Add(new SearchQueryTokenValue(false, tokenSearchType, code, null));
          }
        }
      }

      if (ValueList.Count > 1)
      {
        this.HasLogicalOrProperties = true;
      }

      if (ValueList.Count != 0)
      {
        return;
      }
      
      this.InvalidMessage = $"Unable to parse any values into a {this.GetType().Name} from the string: {values}.";
      this.IsValid = false;
    }
    
  }

