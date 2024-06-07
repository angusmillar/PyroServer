using System.Text;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryComposite(SearchParameterProjection searchParameter, FhirResourceTypeId resourceContext, string rawValue) : SearchQueryBase(searchParameter, resourceContext, rawValue)
{
  public List<SearchQueryCompositeValue> ValueList { get; set; } = new();

  public override object CloneDeep()
  {
    var clone = new SearchQueryComposite(this.SearchParameter, this.ResourceTypeContext, this.RawValue);
    base.CloneDeep(clone);
    clone.ValueList = new List<SearchQueryCompositeValue>();
    clone.ValueList.AddRange(this.ValueList);
    return clone;
  }

  public void ParseCompositeValue(List<SearchQueryBase> searchParameterBaseList, string values)
  {
    ValueList = new List<SearchQueryCompositeValue>();
    foreach (string value in values.Split(SearchQueryBase.OrDelimiter))
    {
      //var DtoSearchParameterCompositeValue = new SearchQueryCompositeValue();
      if (this.Modifier.HasValue && this.Modifier == SearchModifierCodeId.Missing)
      {
        bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
        if (isMissing.HasValue)
        {
          ValueList.Add(new SearchQueryCompositeValue(isMissing.Value));
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
        string[] compositeSplit = value.Split(SearchQueryBase.CompositeDelimiter);
        if (compositeSplit.Count() != searchParameterBaseList.Count)
        {
          StringBuilder sb = new StringBuilder();
          sb.Append($"The SearchParameter {this.SearchParameter.Code} is a Composite type search parameter which means it expects more than a single search value where each value must be separated by a '{SearchQueryBase.CompositeDelimiter}' delimiter character. " +
                    $"However, this instance provided had more or less values than is required for the search parameter used. " +
                    $"{this.SearchParameter.Code} expects {searchParameterBaseList.Count} values yet {compositeSplit.Length} found. ");
          sb.Append("The values expected for this parameter are as follows: ");
          int counter = 1;
          foreach (var item in searchParameterBaseList)
          {
            string resourceNameList = string.Empty;
            foreach (var searchParameterResourceType in item.SearchParameter.BaseList)
            {
              resourceNameList += $"{searchParameterResourceType.ResourceType.GetCode()}, ";
            }

            sb.Append($"Value: {counter.ToString()} is to be a {item.SearchParameter.Type.GetCode()} search parameter type as per the single search parameter '{item.SearchParameter.Code}' for the resource types {resourceNameList}. ");
            counter++;
          }
          this.InvalidMessage = sb.ToString();
          this.IsValid = false;
          break;
        }

        var searchQueryCompositeValue = new SearchQueryCompositeValue(false);
        for (int i = 0; i < compositeSplit.Length; i++)
        {
          searchParameterBaseList[i].RawValue = searchParameterBaseList[i].SearchParameter.Code + ParameterValueDelimiter + compositeSplit[i];
          searchParameterBaseList[i].ParseValue(compositeSplit[i]);
          if (!searchParameterBaseList[i].IsValid)
          {
            string resourceNameList = string.Empty;
            foreach (var searchParameterResourceType in searchParameterBaseList[i].SearchParameter.BaseList)
            {
              resourceNameList += $"{searchParameterResourceType.ResourceType.GetCode()}, ";
            }

            int itemCount = i + 1;
            string error = $"Value: {itemCount.ToString()} is to be a {searchParameterBaseList[i].SearchParameter.Type.GetCode()} search parameter type as per the single search parameter '{searchParameterBaseList[i].SearchParameter.Code}' for the resource type {resourceNameList}. " +
                           $"However, an error was found in parsing its value. Further information: {searchParameterBaseList[i].InvalidMessage}";
            this.InvalidMessage = error;
            this.IsValid = false;
            break;
          }
          searchQueryCompositeValue.SearchQueryBaseList.Add(searchParameterBaseList[i]);
        }
        ValueList.Add(searchQueryCompositeValue);
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

  public override void ParseValue(string values)
  {
    throw new ApplicationException("Internal Server Error: Composite Search Parameters values must be parsed with the specialized method 'TryParseCompositeValue'");
  }

}
