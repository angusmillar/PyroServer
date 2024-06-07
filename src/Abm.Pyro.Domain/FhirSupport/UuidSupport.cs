using System.Text.RegularExpressions;

namespace Abm.Pyro.Domain.FhirSupport;

public class UuidSupport
{
  public const string PATTERN = "urn:uuid:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
  public static bool IsValidValue(string value)
  {
    return Regex.IsMatch(value, "^" + UuidSupport.PATTERN + "$", RegexOptions.Singleline);
  }
}
