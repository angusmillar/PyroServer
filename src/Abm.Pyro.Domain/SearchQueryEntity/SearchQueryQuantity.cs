using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryQuantity(SearchParameterProjection searchParameter, FhirResourceTypeId resourceTypeContext, string rawValue) : SearchQueryBase(searchParameter, resourceTypeContext, rawValue)
{
  private const char VerticalBarDelimiter = '|';
  public List<SearchQueryQuantityValue> ValueList { get; set; } = new();

  public override object CloneDeep()
  {
    var clone = new SearchQueryQuantity(SearchParameter, this.ResourceTypeContext, this.RawValue);
    base.CloneDeep(clone);
    clone.ValueList = new List<SearchQueryQuantityValue>();
    clone.ValueList.AddRange(ValueList);
    return clone;
  }


  public override void ParseValue(string values)
  {
    this.IsValid = true;
    ValueList.Clear();
    foreach (var value in values.Split(SearchQueryBase.OrDelimiter))
    {
      if (this.Modifier.HasValue && this.Modifier == SearchModifierCodeId.Missing)
      {
        bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
        if (isMissing.HasValue)
        {
          ValueList.Add(new SearchQueryQuantityValue(isMissing.Value, null, null, null, null, null, null));
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
        //Examples:
        //Syntax: [parameter]=[prefix][number]|[system]|[code] matches a quantity with the given unit    
        //Observation?value=5.4|http://unitsofmeasure.org|mg
        //Observation?value=5.4||mg
        //Observation?value=le5.4|http://unitsofmeasure.org|mg
        //Observation?value=ap5.4|http://unitsofmeasure.org|mg

        //Observation?value=ap5.4
        //Observation?value=ap5.4|
        //Observation?value=ap5.4|http://unitsofmeasure.org
        //Observation?value=ap5.4|http://unitsofmeasure.org|

        string[] split = value.Trim().Split(VerticalBarDelimiter);
        SearchComparatorId? prefix = SearchQueryValuePrefixBase.GetPrefix(split[0]);
        if (!SearchQueryValuePrefixBase.ValidatePreFix(this.SearchParameter.Type, prefix) && prefix.HasValue)
        {
          this.InvalidMessage = $"The search parameter had an unsupported prefix of '{prefix.Value.GetCode()}'. ";
          this.IsValid = false;
          break;
        }
        string numberAsString = SearchQueryValuePrefixBase.RemovePrefix(split[0], prefix).Trim();
        if (split.Count() == 1)
        {
          if (Decimal.TryParse(numberAsString, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out decimal tempDecimal))
          {
            DecimalInfo decimalInfo = DecimalSupport.GetDecimalInfo(tempDecimal);
            var dtoSearchParameterNumber = new SearchQueryQuantityValue(false,
                                                                        prefix,
                                                                        null,
                                                                        null,
                                                                        decimalInfo.Precision,
                                                                        decimalInfo.Scale,
                                                                        tempDecimal);
            ValueList.Add(dtoSearchParameterNumber);
          }
          else
          {
            this.InvalidMessage = $"Expected a Quantity value yet was unable to parse the provided value '{numberAsString}' as a Decimal. ";
            this.IsValid = false;
            break;
          }
        }
        else if (split.Count() == 2)
        {
          if (Decimal.TryParse(numberAsString, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out decimal tempDecimal))
          {
            string? system;
            if (!string.IsNullOrWhiteSpace(split[1].Trim()))
            {
              system = split[1].Trim();
            }
            else
            {
              system = null;
            }
            DecimalInfo decimalInfo = DecimalSupport.GetDecimalInfo(tempDecimal);
            var dtoSearchParameterNumber = new SearchQueryQuantityValue(false,
                                                                        prefix,
                                                                        system,
                                                                        null,
                                                                        decimalInfo.Precision,
                                                                        decimalInfo.Scale,
                                                                        tempDecimal);

            ValueList.Add(dtoSearchParameterNumber);
          }
          else
          {
            this.InvalidMessage = $"Expected a Quantity value yet was unable to parse the provided value '{numberAsString}' as a Decimal. ";
            this.IsValid = false;
            break;
          }
        }
        else if (split.Count() == 3)
        {
          if (Decimal.TryParse(numberAsString, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out decimal tempDecimal))
          {
            string? system = null;
            if (!string.IsNullOrWhiteSpace(split[1].Trim()))
            {
              system = split[1].Trim();
            }

            string? code = null;
            if (!string.IsNullOrWhiteSpace(split[2].Trim()))
            {
              code = split[2].Trim();
            }
            DecimalInfo decimalInfo = DecimalSupport.GetDecimalInfo(tempDecimal);
            var dtoSearchParameterNumber = new SearchQueryQuantityValue(false,
                                                                        prefix,
                                                                        system,
                                                                        code,
                                                                        decimalInfo.Precision,
                                                                        decimalInfo.Scale,
                                                                        tempDecimal);

            ValueList.Add(dtoSearchParameterNumber);
          }
          else
          {
            this.InvalidMessage = $"Expected a Quantity value yet was unable to parse the provided value '{numberAsString}' as a Decimal. ";
            this.IsValid = false;
            break;
          }
        }
        else
        {
          this.InvalidMessage = $"Expected a Quantity value type yet found to many {SearchQueryQuantity.VerticalBarDelimiter} Delimiters. ";
          this.IsValid = false;
          break;
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
    this.InvalidMessage = $"Unable to parse any values into a {GetType().Name} from the string: {values}.";
    this.IsValid = false;
  }
}
