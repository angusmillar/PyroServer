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
    public List<SearchParameterDto> SearchParameterDtoList { get; set; }
    public List<string> SearchParameterStoreComparatorLineList { get; set; }
    public List<string> SearchParameterStoreBaseLineList { get; set; }
    public List<string> SearchParameterStoreTargetLineList { get; set; }
    public List<string> SearchParameterStoreModifierLineList { get; set; }
    
    public List<string> SearchParameterStoreComponentLineList { get; set; }
    
    public void Load()
    {
      SearchParametersBundleLoader searchParametersBundleLoader = new SearchParametersBundleLoader();
      Bundle bundle = searchParametersBundleLoader.Load();
      
      SearchParameterDtoList = new List<SearchParameterDto>();
      SearchParameterStoreBaseLineList = new List<string>();
      SearchParameterStoreComparatorLineList = new List<string>();
      SearchParameterStoreTargetLineList = new List<string>();
      SearchParameterStoreModifierLineList = new List<string>();
      SearchParameterStoreComponentLineList= new List<string>();
      
      int searchParameterCounter = 1;
      int comparatorCount = 1;
      int baseCount = 1;
      int targetCount = 1;
      int modifierCount = 1;
      int componentCount = 1;
      
      foreach (var entry in bundle.Entry)
      {
        if (entry.Resource is SearchParameter searchParameter)
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
            SearchParameterStoreBaseLineList.Add($"new SearchParameterStoreResourceTypeBase(searchParameterStoreResourceTypeBaseId: {baseCount}, searchParameterStoreId: {searchParameterCounter}, resourceType: FhirResourceTypeId.{resourceType.GetLiteral()}),");
            baseCount++;
          }
          
          var targetList = new List<string>();
          foreach (Hl7.Fhir.Model.ResourceType? resourceType in searchParameter.Target)
          {
            if (resourceType is null)
            {
              throw new ApplicationException($"Found Target of null in SearchParameter id {searchParameter.Id}");
            }
            //new SearchParameterStoreResourceTypeTarget(searchParameterStoreResourceTypeTargetId: 1, searchParameterStoreId: 2, FhirResourceTypeId: resourceType.Patient),
            SearchParameterStoreTargetLineList.Add($"new SearchParameterStoreResourceTypeTarget({targetCount}, {searchParameterCounter}, FhirResourceTypeId.{resourceType.GetLiteral()}),");
            targetCount++;
          }
          
          foreach (var comparator in searchParameter.Comparator)
          {
            //new SearchParameterStoreComparator(searchParameterStoreComparatorId: 1, searchParameterStoreId: 2, searchComparatorId: SearchComparatorId.Ap)
            SearchParameterStoreComparatorLineList.Add($"new SearchParameterStoreComparator({comparatorCount}, {searchParameterCounter}, SearchComparatorId.{comparator.ToString()}),");
            comparatorCount++;
          }
          
          foreach (var modifier in searchParameter.Modifier)
          {
            //new SearchParameterStoreSearchModifierCode(searchParameterStoreSearchModifierCodeId: 1, searchParameterStoreId: 2, searchModifierCodeId: SearchModifierCodeId.Missing),
            SearchParameterStoreModifierLineList.Add($"new SearchParameterStoreSearchModifierCode({modifierCount}, {searchParameterCounter}, SearchModifierCodeId.{modifier}),");
            modifierCount++;
          }

          foreach (var component in searchParameter.Component)
          {
            //new SearchParameterStoreComponent(searchParameterStoreComponentId : 1, searchParameterStoreId: 2, definition: new Uri("http://something"), expression: "value.as(Quantity) | value.as(Range),"),
            SearchParameterStoreComponentLineList.Add($"new SearchParameterStoreComponent({componentCount}, {searchParameterCounter}, new Uri(\"{component.Definition}\"), \"{component.Expression}\"),");
            componentCount++;
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
          
          var param = new SearchParameterDto(
            searchParameterStoreId: searchParameterCounter,
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
            json: $"\"{json}\""
          );
          
          SearchParameterDtoList.Add(param);
          searchParameterCounter++;
          //break;
        }
        else
        {
          throw new AggregateException("Encountered a resource in the SearchParameter bundle that was nt a SearchParameter resource type");
        }
      }
    }
    
    public string ToJson(Resource resource)
    {
      var settings = new FhirJsonPocoSerializerSettings();

      var options = new JsonSerializerOptions().ForFhir(typeof(Resource).Assembly, settings);
      options.WriteIndented = false;

      return JsonSerializer.Serialize(resource, options);
    }
  }
}
