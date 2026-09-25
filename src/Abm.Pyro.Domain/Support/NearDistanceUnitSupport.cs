namespace Abm.Pyro.Domain.Support;

/// <summary>
/// The distance units accepted by the Location 'near' search parameter.
/// FHIR R4 specifies UCUM codes and says kilometres are assumed when units are omitted.
/// This server deliberately supports metres, kilometres and miles only.
/// </summary>
public enum NearDistanceUnit
{
  Metre,
  Kilometre,
  Mile
}

public static class NearDistanceUnitSupport
{
  private const double MetresPerKilometre = 1000d;
  private const double MetresPerMile = 1609.344d;

  public const string SupportedUnitsMessage =
    "Supported units are 'm' (metres), 'km' (kilometres) and '[mi_i]', 'mi', 'mile' or 'miles' (miles). " +
    "When the units are omitted, kilometres are assumed.";

  /// <summary>
  /// Parses a unit code from the fourth segment of a 'near' search parameter value.
  /// A null or empty code means kilometres, per the FHIR R4 specification.
  /// </summary>
  public static bool TryParse(string? unitCode, out NearDistanceUnit unit)
  {
    if (string.IsNullOrWhiteSpace(unitCode))
    {
      unit = NearDistanceUnit.Kilometre;
      return true;
    }

    switch (unitCode.Trim().ToLowerInvariant())
    {
      case "m":
        unit = NearDistanceUnit.Metre;
        return true;
      case "km":
        unit = NearDistanceUnit.Kilometre;
        return true;
      case "[mi_i]":
      case "mi":
      case "mile":
      case "miles":
        unit = NearDistanceUnit.Mile;
        return true;
      default:
        unit = NearDistanceUnit.Kilometre;
        return false;
    }
  }

  public static double ToMetres(decimal distance, NearDistanceUnit unit)
  {
    double value = (double)distance;
    return unit switch
    {
      NearDistanceUnit.Metre => value,
      NearDistanceUnit.Kilometre => value * MetresPerKilometre,
      NearDistanceUnit.Mile => value * MetresPerMile,
      _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };
  }

  public static double FromMetres(double metres, NearDistanceUnit unit)
  {
    return unit switch
    {
      NearDistanceUnit.Metre => metres,
      NearDistanceUnit.Kilometre => metres / MetresPerKilometre,
      NearDistanceUnit.Mile => metres / MetresPerMile,
      _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };
  }

  /// <summary>The UCUM code to place in Quantity.code.</summary>
  public static string UcumCode(NearDistanceUnit unit)
  {
    return unit switch
    {
      NearDistanceUnit.Metre => "m",
      NearDistanceUnit.Kilometre => "km",
      NearDistanceUnit.Mile => "[mi_i]",
      _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };
  }

  /// <summary>The human-readable unit to place in Quantity.unit.</summary>
  public static string DisplayUnit(NearDistanceUnit unit)
  {
    return unit switch
    {
      NearDistanceUnit.Metre => "m",
      NearDistanceUnit.Kilometre => "km",
      NearDistanceUnit.Mile => "mi",
      _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };
  }
}
