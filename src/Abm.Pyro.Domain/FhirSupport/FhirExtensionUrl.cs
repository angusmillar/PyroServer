namespace Abm.Pyro.Domain.FhirSupport;

public static class FhirExtensionUrl
{
  /// <summary>
  /// Carries how far a matched Location is from the point given in a 'near' search. It lives on
  /// Bundle.entry.search rather than in the resource because the value depends on the search,
  /// not on the Location. See https://hl7.org/fhir/R4/location.html#positional
  /// </summary>
  public const string LocationDistance = "http://hl7.org/fhir/StructureDefinition/location-distance";
}
