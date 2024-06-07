using System.Text;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryReference(SearchParameterProjection searchParameter, FhirResourceTypeId resourceTypeContext, IFhirUriFactory fhirUriFactory, string rawValue, bool isChained)
  : SearchQueryBase(searchParameter, resourceTypeContext, rawValue)
{
  public List<SearchQueryReferenceValue> ValueList { get; set; } = new();
  public bool IsChained { get; set; } = isChained;

  public override object CloneDeep()
  {
    var clone = new SearchQueryReference(this.SearchParameter, this.ResourceTypeContext, fhirUriFactory, this.RawValue, this.IsChained);
    base.CloneDeep(clone);
    clone.ValueList = new List<SearchQueryReferenceValue>();
    clone.ValueList.AddRange(ValueList);
    return clone;
  }

  public override void ParseValue(string values)
  {
    this.IsValid = true;
    ValueList = new List<SearchQueryReferenceValue>();
    foreach (string value in values.Split(OrDelimiter))
    {
      if (this.Modifier.HasValue && this.Modifier == SearchModifierCodeId.Missing)
      {
        bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
        if (isMissing.HasValue)
        {
          ValueList.Add(new SearchQueryReferenceValue(isMissing.Value, null));
        }
        else
        {
          this.InvalidMessage = $"Found the {SearchModifierCodeId.Missing.GetCode()} Modifier yet is value was expected to be true or false yet found '{value}'. ";
          this.IsValid = false;
          break;
        }
      }
      else if (!IsChained) // If IsChained then there is no value to check
      {
        SearchQueryReferenceValue? searchQueryReferenceValue;
        if (!value.Contains('/') && !String.IsNullOrWhiteSpace(value.Trim()) && this.SearchParameter.TargetList.Count == 1)
        {
          //If only one allowed Resource type then use this so the reference is just a resource Id '10' and we add on the appropriate ResourceName i.e 'Patient/10'
          string parseValue = $"{this.SearchParameter.TargetList.ToArray()[0].ResourceType.GetCode()}/{value.Trim()}";
          if (fhirUriFactory.TryParse(parseValue, out FhirUri? fhirUri, out string errorMessage))
          {
            searchQueryReferenceValue = new SearchQueryReferenceValue(false, fhirUri);
          }
          else
          {
            this.InvalidMessage = $"The resource reference was {value} and the only allowed resource type was appended to give {parseValue} however parsing of this as a FHIR reference failed with the error: {errorMessage}";
            this.IsValid = false;
            break;
          }
        }
        else if (fhirUriFactory.TryParse(value.Trim(), out FhirUri? fhirUri, out string errorMessage))
        {
          if (String.IsNullOrWhiteSpace(fhirUri!.ResourceName))
          {
            //After parsing the search parameter of type reference if there is no Resource Type for the reference
            //e.g no Patient is the example 'Patient/123456'
            string refResources = string.Empty;
            this.SearchParameter.TargetList.ToList().ForEach(v => refResources += ", " + v.ResourceType.GetCode());
            StringBuilder invalidMessage = new StringBuilder();
            invalidMessage.Append($"The search parameter: {this.SearchParameter.Code} is ambiguous. ");
            invalidMessage.Append($"Additional information: ");
            invalidMessage.Append($"The search parameter: {this.SearchParameter.Code} can reference the following resource types ({refResources.TrimStart(',').Trim()}). ");
            invalidMessage.Append($"To correct this you must postfix the search parameter with a Type modifier, for example: {this.SearchParameter.Code}={this.SearchParameter.TargetList.ToArray()[0].ResourceType.GetCode()}/{fhirUri.ResourceId}");
            invalidMessage.Append($"or: {this.SearchParameter.Code}:{this.SearchParameter.TargetList.ToArray()[0].ResourceType.GetCode()}={fhirUri.ResourceId} ");
            invalidMessage.Append($"If the: {this.SearchParameter.TargetList.ToArray()[0].ResourceType.GetCode()} resource was the intended reference for the search parameter: {this.SearchParameter.Code}.");
            this.IsValid = false;
            this.InvalidMessage = invalidMessage.ToString();
            break;
          }

          //Most likely an absolute or relative so just parse it 
          searchQueryReferenceValue = new SearchQueryReferenceValue(false, fhirUri);
        }
        else
        {
          this.InvalidMessage = $"Unable to parse the reference search parameter of: '{value}'. {errorMessage}";
          this.IsValid = false;
          break;
        }

        if (searchQueryReferenceValue.FhirUri is not null)
        {
          //Check the Resource type we resolved to is allowed for the search parameter            
          if (!String.IsNullOrWhiteSpace(searchQueryReferenceValue.FhirUri.ResourceName) && 
              this.SearchParameter.TargetList.All(x => x.ResourceType.GetCode() != searchQueryReferenceValue.FhirUri.ResourceName))
          {
            this.InvalidMessage = $"The resource name used in the reference search parameter is not allowed for this search parameter type against this resource type.";
            this.IsValid = false;
            break;
          }
          ValueList.Add(searchQueryReferenceValue);
        }
        else
        {
          throw new ArgumentNullException($"Internal Server Error: Either {nameof(searchQueryReferenceValue)} or {nameof(searchQueryReferenceValue.FhirUri)} was found to be null");
        }
      }
    }

    if (ValueList.Count > 1)
    {
      this.HasLogicalOrProperties = true;
    }

    if (ValueList.Count == 0)
    {
      if (!IsChained)
      {
        this.InvalidMessage = $"Unable to parse any values into a {GetType().Name} from the string: {values}.";
        this.IsValid = false;
      }
    }
  }
}
