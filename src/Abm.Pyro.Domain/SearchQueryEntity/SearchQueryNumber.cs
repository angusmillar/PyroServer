using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryNumber(SearchParameterProjection searchParameter, FhirResourceTypeId resourceTypeContext, string rawValue) : SearchQueryBase(searchParameter, resourceTypeContext, rawValue)
{
  public List<SearchQueryNumberValue> ValueList { get; set; } = new();

  public override object CloneDeep()
  {
    var clone = new SearchQueryNumber(SearchParameter, this.ResourceTypeContext, this.RawValue);
    base.CloneDeep(clone);
    clone.ValueList = new List<SearchQueryNumberValue>();
    clone.ValueList.AddRange(ValueList);
    return clone;
  }

  public override void ParseValue(string values)
  {
    this.IsValid = true;
    ValueList = new List<SearchQueryNumberValue>();
    foreach (var value in values.Split(OrDelimiter))
    {
      if (this.Modifier is SearchModifierCodeId.Missing)
      {
        bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
        if (isMissing.HasValue)
        {
          ValueList.Add(new SearchQueryNumberValue(isMissing.Value, null, null, null, null));
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
        SearchComparatorId? prefix = SearchQueryValuePrefixBase.GetPrefix(value);
        if (!SearchQueryValuePrefixBase.ValidatePreFix(this.SearchParameter.Type, prefix) && prefix.HasValue)
        {
          this.InvalidMessage = $"The search parameter had an unsupported prefix of '{prefix.Value.GetCode()}'. ";
          this.IsValid = false;
          break;
        }

        string numberAsString = SearchQueryValuePrefixBase.RemovePrefix(value, prefix);
        if (Decimal.TryParse(numberAsString, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out decimal tempDecimal))
        {
          var decimalInfo = DecimalSupport.GetDecimalInfo(tempDecimal);
          var searchQueryNumberValue = new SearchQueryNumberValue(false,
                                                                  prefix,
                                                                  decimalInfo.Precision,
                                                                  decimalInfo.Scale,
                                                                  tempDecimal);
          ValueList.Add(searchQueryNumberValue);
        }
        else
        {
          this.InvalidMessage = $"Unable to parse the value of : {numberAsString} to a DateTime.";
          this.IsValid = false;
          break;
        }
      }
    }
    if (ValueList.Count > 1)
    {
      this.HasLogicalOrProperties = true;
    }

    if (ValueList.Count == 0)
    {
      this.InvalidMessage = $"Unable to parse any values into a {this.GetType().Name} from the string: {values}.";
      this.IsValid = false;
    }
  }
}
