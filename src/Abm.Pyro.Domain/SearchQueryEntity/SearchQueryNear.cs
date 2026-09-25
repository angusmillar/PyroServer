using System.Globalization;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Support;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Domain.SearchQueryEntity;

/// <summary>
/// Parses the FHIR R4 Location 'near' search parameter, whose value is
/// [latitude]|[longitude]|[distance]|[units], with the distance and units optional.
/// See https://hl7.org/fhir/R4/location.html#positional
/// </summary>
public class SearchQueryNear(
  SearchParameterProjection searchParameter,
  FhirResourceTypeId resourceTypeContext,
  string rawValue,
  LocationNearSettings locationNearSettings)
  : SearchQueryBase(searchParameter, resourceTypeContext, rawValue)
{
  private const char VerticalBarDelimiter = '|';
  private const double MinimumLatitude = -90d;
  private const double MaximumLatitude = 90d;
  private const double MinimumLongitude = -180d;
  private const double MaximumLongitude = 180d;

  public List<SearchQueryNearValue> ValueList { get; set; } = new();

  public override object CloneDeep()
  {
    var clone = new SearchQueryNear(SearchParameter, ResourceTypeContext, RawValue, locationNearSettings);
    base.CloneDeep(clone);
    clone.ValueList = new List<SearchQueryNearValue>();
    clone.ValueList.AddRange(ValueList);
    return clone;
  }

  public override Task ParseValue(string values)
  {
    IsValid = true;
    ValueList.Clear();

    foreach (string value in values.Split(OrDelimiter))
    {
      if (Modifier.HasValue && Modifier == SearchModifierCodeId.Missing)
      {
        if (!ParseMissingValue(value))
        {
          break;
        }

        continue;
      }

      if (!ParsePositionValue(value))
      {
        break;
      }
    }

    return Task.CompletedTask;
  }

  private bool ParseMissingValue(string value)
  {
    bool? isMissing = SearchQueryValueBase.ParseModifierEqualToMissing(value);
    if (isMissing.HasValue)
    {
      ValueList.Add(new SearchQueryNearValue(isMissing.Value, 0d, 0d, 0d, NearDistanceUnit.Kilometre));
      return true;
    }

    InvalidMessage = $"Found the {SearchModifierCodeId.Missing.GetCode()} Modifier yet its value was expected to be true or false yet found '{value}'. ";
    IsValid = false;
    return false;
  }

  private bool ParsePositionValue(string value)
  {
    // The value is [latitude]|[longitude]|[distance]|[units]. Note that the order is latitude
    // then longitude. The worked example in the FHIR R4 specification at
    // https://hl7.org/fhir/R4/location.html#positional has the two transposed; it is a known
    // erratum and the normative parameter description is followed here.
    string[] split = value.Trim().Split(VerticalBarDelimiter);

    if (split.Length is < 2 or > 4)
    {
      InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' was expected to be of the form " +
                       $"[latitude]|[longitude]|[distance]|[units] where the distance and units are optional, " +
                       $"yet {split.Length.ToString()} vertical-bar separated segments were found. ";
      IsValid = false;
      return false;
    }

    if (!TryParseCoordinate(split[0], MinimumLatitude, MaximumLatitude, "latitude", value, out double latitude))
    {
      return false;
    }

    if (!TryParseCoordinate(split[1], MinimumLongitude, MaximumLongitude, "longitude", value, out double longitude))
    {
      return false;
    }

    string? unitCode = split.Length == 4 ? split[3] : null;
    if (!NearDistanceUnitSupport.TryParse(unitCode, out NearDistanceUnit unit))
    {
      InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' had an unsupported distance unit of '{unitCode}'. " +
                       $"{NearDistanceUnitSupport.SupportedUnitsMessage} ";
      IsValid = false;
      return false;
    }

    string distanceAsString = split.Length >= 3 ? split[2].Trim() : string.Empty;
    double distanceInMetres;

    if (distanceAsString.Length == 0)
    {
      // FHIR R4 leaves the radius to the server's discretion when the distance is omitted.
      distanceInMetres = locationNearSettings.DefaultDistanceInMetres;
    }
    else
    {
      if (!decimal.TryParse(distanceAsString, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal distance))
      {
        InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' had a distance of '{distanceAsString}' which could not be parsed as a number. ";
        IsValid = false;
        return false;
      }

      if (distance <= 0m)
      {
        InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' had a distance of '{distanceAsString}' which must be greater than zero. ";
        IsValid = false;
        return false;
      }

      distanceInMetres = NearDistanceUnitSupport.ToMetres(distance, unit);

      if (distanceInMetres > locationNearSettings.MaximumDistanceInMetres)
      {
        InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' requested a distance of {distanceInMetres.ToString(CultureInfo.InvariantCulture)} metres " +
                         $"which is greater than the maximum this server supports of {locationNearSettings.MaximumDistanceInMetres.ToString(CultureInfo.InvariantCulture)} metres. ";
        IsValid = false;
        return false;
      }
    }

    ValueList.Add(new SearchQueryNearValue(false, latitude, longitude, distanceInMetres, unit));
    return true;
  }

  private bool TryParseCoordinate(string segment, double minimum, double maximum, string coordinateName, string value, out double coordinate)
  {
    if (!double.TryParse(segment.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate))
    {
      InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' had a {coordinateName} of '{segment}' which could not be parsed as a number. " +
                       $"Note that the decimal separator must be a full stop, because a comma separates multiple positions. ";
      IsValid = false;
      return false;
    }

    if (!double.IsFinite(coordinate))
    {
      InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' had a {coordinateName} of '{segment}' which could not be parsed as a number. " +
                       $"Note that the decimal separator must be a full stop, because a comma separates multiple positions. ";
      IsValid = false;
      return false;
    }

    if (coordinate < minimum || coordinate > maximum)
    {
      InvalidMessage = $"The '{SearchParameter.Code}' search parameter value '{value}' had a {coordinateName} of '{segment}' which is outside the valid range of " +
                       $"{minimum.ToString(CultureInfo.InvariantCulture)} to {maximum.ToString(CultureInfo.InvariantCulture)}. ";
      IsValid = false;
      return false;
    }

    return true;
  }
}
