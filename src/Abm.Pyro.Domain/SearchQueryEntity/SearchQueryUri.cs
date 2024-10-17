using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryUri(SearchParameterProjection searchParameter, FhirResourceTypeId resourceTypeContext, string rawValue) : SearchQueryBase(searchParameter, resourceTypeContext, rawValue)
{
  public List<SearchQueryUriValue> ValueList { get; set; } = new();

  public override object CloneDeep()
  {
    var clone = new SearchQueryUri(SearchParameter, this.ResourceTypeContext, this.RawValue);
    base.CloneDeep(clone);
    clone.ValueList = new List<SearchQueryUriValue>();
    clone.ValueList.AddRange(this.ValueList);
    return clone;
  }

  public override Task ParseValue(string values)
  {
    IsValid = true;
    ValueList.Clear();
    foreach (string value in values.Split(OrDelimiter))
    {
      ProcessValue(value);
      if (!IsValid)
      {
        break;
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
    
    return Task.CompletedTask;;
  }

  private void ProcessValue(string value)
  {
    if (Modifier is SearchModifierCodeId.Missing)
    {
      bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
      if (!isMissing.HasValue)
      {
        InvalidMessage =
          $"Found the {SearchModifierCodeId.Missing.GetCode()} Modifier yet is value was expected to be true or false yet found '{value}'. ";
        IsValid = false;
        return;
      }
      
      ValueList.Add(new SearchQueryUriValue(isMissing.Value, null));
      
    }
    else
    {
      if (!Uri.TryCreate(value.Trim(), UriKind.RelativeOrAbsolute, out Uri? tempUri))
      {
        InvalidMessage = $"Unable to parse the given URI search parameter string of : {value.Trim()}";
        IsValid = false;
        return;
      }
      
      ValueList.Add(new SearchQueryUriValue(false, tempUri));
      
    }
  }
  
}
