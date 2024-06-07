using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum FhirFormatType 
{
  [EnumInfo("json", "application/fhir+xml" )]
  Json,
  [EnumInfo("xml", "application/fhir+json")]
  Xml    
};
