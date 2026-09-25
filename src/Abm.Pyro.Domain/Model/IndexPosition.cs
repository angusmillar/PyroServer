using NetTopologySuite.Geometries;

#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class IndexPosition : IndexBase
{
  private IndexPosition() : base()
  {
  }

  public IndexPosition(int? indexPositionId, int? resourceStoreId, ResourceStore? resourceStore,
                       int? searchParameterStoreId, SearchParameterStore? searchParameterStore,
                       Point position)
    : base(resourceStoreId, resourceStore, searchParameterStoreId, searchParameterStore)
  {
    IndexPositionId = indexPositionId;
    Position = position;
  }

  public int? IndexPositionId { get; set; }

  /// <summary>
  /// The Location's WGS84 position, stored as a SQL Server geography point with SRID 4326.
  /// Note the coordinate order: X is longitude and Y is latitude, which is the opposite
  /// of T-SQL's geography::Point(latitude, longitude, srid).
  /// </summary>
  public Point Position { get; set; }
}
