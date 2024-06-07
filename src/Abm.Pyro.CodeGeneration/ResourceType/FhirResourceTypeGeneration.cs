using System.Collections.Generic;
using Hl7.Fhir.Model;
namespace Abm.Pyro.CodeGeneration.ResourceType
{
  public class FhirResourceTypeGeneration
  {
    public Dictionary<string, int> Dic { get; set; }
    public FhirResourceTypeGeneration()
    {
      Dic = new Dictionary<string, int>();
      var counter = 1;
      Dic.Add("Resource", counter);
      counter++;
      Dic.Add("DomainResource", counter);
      counter++;
      
      foreach (string resourceName in ModelInfo.SupportedResources)
      {
        if (!Dic.ContainsKey(resourceName))
        {
          Dic.Add(resourceName, counter);
          counter++;
        }
      }
    }
  }
}
