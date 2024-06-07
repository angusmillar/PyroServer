namespace Abm.Pyro.Domain.FhirSupport;

public static class GuidSupport
{
  private const string FhirGuidFormat = "D"; 
  /// <summary>
  /// Create a new random FHIR guid
  /// </summary>
  /// <returns></returns>
  public static string NewFhirGuid()
  {
    return ToFhirGuid(Guid.NewGuid());
  }

  /// <summary>
  /// Formats a GUID instance to a FHIR Guid formatted string
  /// </summary>
  /// <param name="guid"></param>
  /// <returns></returns>
  public static string ToFhirGuid(Guid guid)
  {
    return guid.ToString(FhirGuidFormat);
  }
  /// <summary>
  /// Test that the string is a FHIR guid format, returns true if it is
  /// </summary>
  /// <param name="guid"></param>
  /// <returns></returns>
  public static bool IsFhirGuid(string guid)
  {
    return Guid.TryParseExact(guid, FhirGuidFormat, out _);
  }
}
