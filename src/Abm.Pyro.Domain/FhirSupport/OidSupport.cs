using System.Text.RegularExpressions;

namespace Abm.Pyro.Domain.FhirSupport;

public class OidSupport
{
  public const string PATTERN = @"urn:oid:[0-2](\.(0|[1-9][0-9]*))+";
  public static bool IsValidValue(string value)
  {
    return Regex.IsMatch(value, "^" + OidSupport.PATTERN + "$", RegexOptions.Singleline);
  }
}
