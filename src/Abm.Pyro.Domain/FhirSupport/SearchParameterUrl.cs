namespace Abm.Pyro.Domain.FhirSupport;

/// <summary>
/// Canonical URLs of search parameters that need bespoke handling.
/// </summary>
public static class SearchParameterUrl
{
  /// <summary>
  /// The Location 'near' search parameter: the only search parameter of type 'special' in the
  /// FHIR R4 search parameter set, and the only one this server supports.
  /// </summary>
  public const string LocationNear = "http://hl7.org/fhir/SearchParameter/Location-near";
}
