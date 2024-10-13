using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum FhirFormatType 
{
  [EnumInfo("json", "application/fhir+json" )]
  Json,
  [EnumInfo("xml", "application/fhir+xml")]
  Xml    
};
