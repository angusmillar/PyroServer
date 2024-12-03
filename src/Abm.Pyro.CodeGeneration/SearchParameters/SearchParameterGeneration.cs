using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Abm.Pyro.CodeGeneration.Support;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;

namespace Abm.Pyro.CodeGeneration.SearchParameters
{
  public class SearchParameterGeneration
  {
    private List<ExcludedSearchParameter> _excludedSearchParameterList = new List<ExcludedSearchParameter>()
    {
      new ExcludedSearchParameter(resourceType: Hl7.Fhir.Model.ResourceType.DomainResource, code: "_text"),
      new ExcludedSearchParameter(resourceType: Hl7.Fhir.Model.ResourceType.Resource, code: "_content"),
      new ExcludedSearchParameter(resourceType: Hl7.Fhir.Model.ResourceType.Resource, code: "_query"),
      new ExcludedSearchParameter(resourceType: Hl7.Fhir.Model.ResourceType.Resource, code: "_source"),
    };
    
    public List<SearchParameterDto> SearchParameterDtoList { get; } = new List<SearchParameterDto>();
    public List<string> SearchParameterStoreComparatorLineList { get; } = new List<string>();
    public List<string> SearchParameterStoreBaseLineList { get; } = new List<string>();
    public List<string> SearchParameterStoreTargetLineList { get; } = new List<string>();
    public List<string> SearchParameterStoreModifierLineList { get; } = new List<string>();
    public List<string> SearchParameterStoreComponentLineList { get; } = new List<string>();

    private int _searchParameterCounter = 1;
    private int _comparatorCount = 1;
    private int _baseCount = 1;
    private int _targetCount = 1;
    private int _modifierCount = 1;
    private int _componentCount = 1;

    public void Load()
    {
      SearchParametersBundleLoader searchParametersBundleLoader = new SearchParametersBundleLoader();
      Bundle bundle = searchParametersBundleLoader.Load();
      
      foreach (var entry in bundle.Entry)
      {
        if (entry.Resource is SearchParameter searchParameter)
        {
          if (IsSearchParameterSupported(searchParameter))
          {
            ProcessSearchParameter(searchParameter);    
          }
        }
        else
        {
          throw new AggregateException("Encountered a resource in the SearchParameter bundle that was nt a SearchParameter resource type");
        }
      }
    }

    private bool IsSearchParameterSupported(SearchParameter searchParameter)
    {
      foreach (ExcludedSearchParameter excludedSearchParameter in _excludedSearchParameterList)
      {
        if (searchParameter.Base.Contains(excludedSearchParameter.ResourceType) &&
            searchParameter.Code.Equals(excludedSearchParameter.Code, StringComparison.OrdinalIgnoreCase))
        {
          return false;
        }
      }

      return true;
    }
    
    private void ProcessSearchParameter(
      SearchParameter searchParameter)
    {
      if (searchParameter.Chain.Any())
      {
        throw new ApplicationException($"Found chain in SearchParameter id {searchParameter.Id} of: " + string.Join(",", searchParameter.Chain.ToArray()));
      }
      
      foreach (Hl7.Fhir.Model.ResourceType? resourceType in searchParameter.Base)
      {
        if (resourceType is null)
        {
          throw new ApplicationException($"Found Base resourceType of null in SearchParameter id {searchParameter.Id}");
        }
            
        //new SearchParameterStoreResourceTypeBase(searchParameterStoreResourceTypeBaseId: 1, searchParameterStoreId: 2, resourceType : FhirResourceTypeId.Patient),
        SearchParameterStoreBaseLineList.Add($"new SearchParameterStoreResourceTypeBase(searchParameterStoreResourceTypeBaseId: {_baseCount}, searchParameterStoreId: {_searchParameterCounter}, resourceType: FhirResourceTypeId.{resourceType.GetLiteral()}),");
        _baseCount++;
      }
      
      foreach (Hl7.Fhir.Model.ResourceType? resourceType in searchParameter.Target)
      {
        if (resourceType is null)
        {
          throw new ApplicationException($"Found Target of null in SearchParameter id {searchParameter.Id}");
        }
        //new SearchParameterStoreResourceTypeTarget(searchParameterStoreResourceTypeTargetId: 1, searchParameterStoreId: 2, FhirResourceTypeId: resourceType.Patient),
        SearchParameterStoreTargetLineList.Add($"new SearchParameterStoreResourceTypeTarget({_targetCount}, {_searchParameterCounter}, FhirResourceTypeId.{resourceType.GetLiteral()}),");
        _targetCount++;
      }
          
      foreach (var comparator in searchParameter.Comparator)
      {
        //new SearchParameterStoreComparator(searchParameterStoreComparatorId: 1, searchParameterStoreId: 2, searchComparatorId: SearchComparatorId.Ap)
        SearchParameterStoreComparatorLineList.Add($"new SearchParameterStoreComparator({_comparatorCount}, {_searchParameterCounter}, SearchComparatorId.{comparator.ToString()}),");
        _comparatorCount++;
      }
          
      foreach (var modifier in searchParameter.Modifier)
      {
        //new SearchParameterStoreSearchModifierCode(searchParameterStoreSearchModifierCodeId: 1, searchParameterStoreId: 2, searchModifierCodeId: SearchModifierCodeId.Missing),
        SearchParameterStoreModifierLineList.Add($"new SearchParameterStoreSearchModifierCode({_modifierCount}, {_searchParameterCounter}, SearchModifierCodeId.{modifier}),");
        _modifierCount++;
      }

      foreach (var component in searchParameter.Component)
      {
        //new SearchParameterStoreComponent(searchParameterStoreComponentId : 1, searchParameterStoreId: 2, definition: new Uri("http://something"), expression: "value.as(Quantity) | value.as(Range),"),
        SearchParameterStoreComponentLineList.Add($"new SearchParameterStoreComponent({_componentCount}, {_searchParameterCounter}, new Uri(\"{component.Definition}\"), \"{component.Expression}\"),");
        _componentCount++;
      }
          
      string multipleOr = "null";
      if (searchParameter.MultipleOr != null)
      {
        multipleOr = searchParameter.MultipleOr.ToString().ToLower();
      }
          
      string multipleAnd = "null";
      if (searchParameter.MultipleAnd != null)
      {
        multipleAnd = searchParameter.MultipleAnd.ToString().ToLower();
      }
          
      string expression = "null";
      if (searchParameter.Expression != null)
      {
        expression = $"\"{searchParameter.Expression}\"";
      }

      string json = $"{ToJson(searchParameter).Replace(@"\", @"\\").Replace("\"", "\\\"")}";

      string lastUpdated = "new DateTime(2024, 02, 18, 02, 30, 00, 000)";
          
      var param = new SearchParameterDto(
        searchParameterStoreId: _searchParameterCounter,
        resourceId: $"\"{searchParameter.Id}\"",
        versionId: 1,
        isCurrent: true,
        isDeleted: false,
        name: $"\"{searchParameter.Name}\"",
        status: PublicationStatus.Active.ToString(),
        url: searchParameter.Url,
        code: $"\"{searchParameter.Code}\"",
        type: searchParameter.Type.ToString(),
        expression: expression,
        multipleOr: multipleOr,
        multipleAnd: multipleAnd,
        chain: "null",
        json: $"\"{json}\"",
        lastUpdated: lastUpdated
      );
          
      SearchParameterDtoList.Add(param);
      _searchParameterCounter++;
    }

    public string ToJson(Resource resource)
    {
      var settings = new FhirJsonPocoSerializerSettings();

      var options = new JsonSerializerOptions().ForFhir(typeof(Resource).Assembly, settings);
      options.WriteIndented = false;

      return JsonSerializer.Serialize(resource, options);
    }
    
    private class ExcludedSearchParameter
    {
      public ExcludedSearchParameter(
        Hl7.Fhir.Model.ResourceType resourceType,
        string code)
      {
        ResourceType = resourceType;
        Code = code;
      }

      public Hl7.Fhir.Model.ResourceType ResourceType { get; }
      public string Code { get; }
    }
    
  }
}
