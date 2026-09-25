using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryNearValue(
  bool isMissing,
  double latitude,
  double longitude,
  double distanceInMetres,
  NearDistanceUnit reportUnit)
  : SearchQueryValueBase(isMissing)
{
  public double Latitude { get; } = latitude;
  public double Longitude { get; } = longitude;

  /// <summary>
  /// The search radius in metres. Metres is canonical below the parser because SQL Server's
  /// STDistance returns metres.
  /// </summary>
  public double DistanceInMetres { get; } = distanceInMetres;

  /// <summary>
  /// The unit the client expressed the distance in, retained only so a computed distance can be
  /// reported back in the same unit rather than always in kilometres.
  /// </summary>
  public NearDistanceUnit ReportUnit { get; } = reportUnit;
}
