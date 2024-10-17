using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Domain.SearchQueryEntity;
public class SearchQueryString(SearchParameterProjection searchParameter, FhirResourceTypeId resourceTypeContext, string rawValue) : SearchQueryBase(searchParameter, resourceTypeContext, rawValue)
{
    public List<SearchQueryStringValue> ValueList { get; set; } = new();


    public override object CloneDeep()
    {
      var clone = new SearchQueryString(SearchParameter, ResourceTypeContext, RawValue);
      base.CloneDeep(clone);
      clone.ValueList = new List<SearchQueryStringValue>();
      clone.ValueList.AddRange(ValueList);
      return clone;
    }


    public override Task ParseValue(string values)
    {
      IsValid = true;
      ValueList.Clear();
      foreach (string value in values.Split(OrDelimiter))
      {
        if (Modifier is SearchModifierCodeId.Missing)
        {
          bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
          if (isMissing.HasValue)
          {
            ValueList.Add(new SearchQueryStringValue(isMissing.Value, null));
          }
          else
          {
            InvalidMessage = $"Found the {SearchModifierCodeId.Missing.GetCode()} Modifier yet the value was expected to be true or false yet found '{value}'. ";
            IsValid = false;
            break;
          }
        }
        else
        {
          ValueList.Add(new SearchQueryStringValue(false, StringSupport.ToLowerTrimRemoveDiacriticsTruncate(value, RepositoryModelConstraints.StringMaxLength)));
        }
      }

      if (ValueList.Count > 1)
      {
        HasLogicalOrProperties = true;
      }

      if (ValueList.Count == 0)
      {
        InvalidMessage = $"Unable to parse any values into a {GetType().Name} from the string: {values}.";
        IsValid = false;
      }
      
      return Task.CompletedTask;
    }
  }

