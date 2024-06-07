using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.SearchQueryEntity;

  public class SearchQueryDateTime(SearchParameterProjection searchParameter, FhirResourceTypeId resourceTypeContext, string rawValue, IFhirDateTimeFactory fhirDateTimeFactory)
    : SearchQueryBase(searchParameter, resourceTypeContext, rawValue)
  {
    public List<SearchQueryDateTimeValue> ValueList { get; set; } = new();

    public override object CloneDeep()
    {
      var clone = new SearchQueryDateTime(SearchParameter, this.ResourceTypeContext, this.RawValue, fhirDateTimeFactory);
      base.CloneDeep(clone);
      clone.ValueList = new List<SearchQueryDateTimeValue>();
      clone.ValueList.AddRange(ValueList);
      return clone;
    }

    public override void ParseValue(string values)
    {
      this.IsValid = true;
      ValueList = new List<SearchQueryDateTimeValue>();
      foreach (string value in values.Split(OrDelimiter))
      {
        if (this.Modifier.HasValue && this.Modifier == SearchModifierCodeId.Missing)
        {
          bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
          if (isMissing.HasValue)
          {
            ValueList.Add(new SearchQueryDateTimeValue(isMissing.Value, null, null, null));
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

          string dateTimeStirng = SearchQueryValuePrefixBase.RemovePrefix(value, prefix);
          if (fhirDateTimeFactory.TryParse(dateTimeStirng.Trim(), out DateTimeWithPrecision? dateTimeWithPrecision, out string? errorMessage))
          {
            var searchQueryDateTimeValue = new SearchQueryDateTimeValue(false, prefix, dateTimeWithPrecision!.Precision, dateTimeWithPrecision!.DateTime);
            ValueList.Add(searchQueryDateTimeValue);
          }
          else
          {
            this.InvalidMessage = errorMessage!;
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
      this.InvalidMessage = $"Unable to parse any values into a {this.GetType().Name} from the string: {values}.";
      this.IsValid = false;
    }
  }

