using System.Text;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryReference(
    SearchParameterProjection searchParameter,
    FhirResourceTypeId resourceTypeContext,
    IFhirUriFactory fhirUriFactory,
    string rawValue,
    bool isChained)
    : SearchQueryBase(searchParameter, resourceTypeContext, rawValue)
{
    public List<SearchQueryReferenceValue> ValueList { get; set; } = new();
    public bool IsChained { get; set; } = isChained;

    public override object CloneDeep()
    {
        var clone = new SearchQueryReference(this.SearchParameter, this.ResourceTypeContext, fhirUriFactory,
            this.RawValue, this.IsChained);
        base.CloneDeep(clone);
        clone.ValueList = new List<SearchQueryReferenceValue>();
        clone.ValueList.AddRange(ValueList);
        return clone;
    }

    public override async Task ParseValue(
        string values)
    {
        this.IsValid = true;
        ValueList = new List<SearchQueryReferenceValue>();
        foreach (string value in values.Split(OrDelimiter))
        {
            await ProcessValue(value);
            if (!IsValid)
            {
                break;
            }
        }

        if (ValueList.Count > 1)
        {
            HasLogicalOrProperties = true;
        }

        if (ValueList.Count == 0)
        {
            if (!IsChained)
            {
                InvalidMessage = $"Unable to parse any values into a {GetType().Name} from the string: {values}.";
                IsValid = false;
            }
        }
    }

    private async Task ProcessValue(
        string value)
    {
        if (Modifier is SearchModifierCodeId.Missing)
        {
            ProcessAsMissingModifier(value);
        }
        else if (!IsChained) // If IsChained then there is no value to check
        {
            await ProcessAsNotChained(value);
        }
    }

    private async Task ProcessAsNotChained(string value)
    {
        SearchQueryReferenceValue? searchQueryReferenceValue = null;
        if (!value.Contains('/') && !String.IsNullOrWhiteSpace(value.Trim()) && SearchParameter.TargetList.Count == 1)
        {
            //If only one allowed Resource type then use this so the reference is just a resource ID '10' and we add on the appropriate ResourceName i.e 'Patient/10'
            string parseValue = $"{SearchParameter.TargetList.ToArray()[0].ResourceType.GetCode()}/{value.Trim()}";
            var fhirUriParseOutcome = await fhirUriFactory.TryParse2(parseValue);
            if (!fhirUriParseOutcome.Success)
            {
                InvalidMessage =
                    $"The resource reference was {value} and the only allowed resource type was appended to give " +
                    $"{parseValue} however parsing of this as a FHIR reference failed with the error: {fhirUriParseOutcome.errorMessage}";
                IsValid = false;
                return;
            }

            searchQueryReferenceValue = new SearchQueryReferenceValue(false, fhirUriParseOutcome.fhirUri);
        }

        if (searchQueryReferenceValue is null)
        {
            var fhirUriParseOutcome2 = await fhirUriFactory.TryParse2(value.Trim());
            if (!fhirUriParseOutcome2.Success)
            {
                InvalidMessage =
                    $"Unable to parse the reference search parameter of: '{value}'. {fhirUriParseOutcome2.errorMessage}";
                IsValid = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(fhirUriParseOutcome2.fhirUri!.ResourceName))
            {
                //After parsing the search parameter of type reference if there is no Resource Type for the reference
                //e.g. no Patient is the example 'Patient/123456'
                string refResources = string.Empty;
                this.SearchParameter.TargetList.ToList().ForEach(v => refResources += ", " + v.ResourceType.GetCode());
                StringBuilder invalidMessage = new StringBuilder();
                invalidMessage.Append($"The search parameter: {SearchParameter.Code} is ambiguous. ");
                invalidMessage.Append("Additional information: ");
                invalidMessage.Append($"The search parameter: {SearchParameter.Code} can reference the following " +
                                      $"resource types ({refResources.TrimStart(',').Trim()}). ");
                invalidMessage.Append($"To correct this you must postfix the search parameter with a Type modifier, for " +
                                      $"example: {SearchParameter.Code}={SearchParameter.TargetList.ToArray()[0].ResourceType.GetCode()}/{fhirUriParseOutcome2.fhirUri.ResourceId}");
                invalidMessage.Append($"or: {SearchParameter.Code}:{SearchParameter.TargetList.ToArray()[0].ResourceType.GetCode()}={fhirUriParseOutcome2.fhirUri.ResourceId} ");
                invalidMessage.Append($"If the: {SearchParameter.TargetList.ToArray()[0].ResourceType.GetCode()} resource " +
                                      $"was the intended reference for the search parameter: {SearchParameter.Code}.");
                IsValid = false;
                InvalidMessage = invalidMessage.ToString();
                return;
            }

            //Most likely an absolute or relative so just parse it 
            searchQueryReferenceValue = new SearchQueryReferenceValue(false, fhirUriParseOutcome2.fhirUri);
        }

        if (searchQueryReferenceValue.FhirUri is null)
        {
            throw new ArgumentNullException(
                $"Internal Server Error: Either {nameof(searchQueryReferenceValue)} or " +
                $"{nameof(searchQueryReferenceValue.FhirUri)} was found to be null");
        }

        //Check the Resource type we resolved to is allowed for the search parameter            
        if (!string.IsNullOrWhiteSpace(searchQueryReferenceValue.FhirUri.ResourceName) &&
            SearchParameter.TargetList.All(x =>
                x.ResourceType.GetCode() != searchQueryReferenceValue.FhirUri.ResourceName))
        {
            InvalidMessage = "The resource name used in the reference search parameter is not allowed for this search " +
                             "parameter type against this resource type.";
            IsValid = false;
            return;
        }

        ValueList.Add(searchQueryReferenceValue);
    }
    
    
    private void ProcessAsMissingModifier(
        string value)
    {
        bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
        if (!isMissing.HasValue)
        {
            InvalidMessage =
                $"Found the {SearchModifierCodeId.Missing.GetCode()} Modifier yet is value was expected to be " +
                $"true or false yet found '{value}'. ";
            IsValid = false;
        }

        ValueList.Add(new SearchQueryReferenceValue(isMissing!.Value, null));
    }

    
}