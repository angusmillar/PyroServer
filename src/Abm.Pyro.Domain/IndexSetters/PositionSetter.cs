using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Abm.Pyro.Domain.IndexSetters;

public class PositionSetter(ILogger<PositionSetter> logger) : IPositionSetter
{
  private const decimal MinimumLatitude = -90m;
  private const decimal MaximumLatitude = 90m;
  private const decimal MinimumLongitude = -180m;
  private const decimal MaximumLongitude = 180m;
  private const int Wgs84Srid = 4326;

  private FhirResourceTypeId ResourceType;
  private int SearchParameterId;
  private string? SearchParameterName;

  public IList<IndexPosition> Set(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName)
  {
    ResourceType = resourceType;
    SearchParameterId = searchParameterId;
    SearchParameterName = searchParameterName;

    if (typedElement is not IFhirValueProvider fhirValueProvider)
    {
      throw new NullReferenceException($"ITypedElement was expected to implement IFhirValueProvider for the SearchParameter entity with the database " +
                                       $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                       $"name of: {SearchParameterName}");
    }

    if (fhirValueProvider.FhirValue is null)
    {
      throw new NullReferenceException($"FhirValueProvider's FhirValue found to be null for the SearchParameter entity with the database " +
                                       $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                       $"name of: {SearchParameterName}");
    }

    return ProcessFhirDataType(fhirValueProvider.FhirValue);
  }

  private IList<IndexPosition> ProcessFhirDataType(Base fhirValue)
  {
    if (fhirValue is not Hl7.Fhir.Model.Location.PositionComponent position)
    {
      throw new FormatException($"Unknown FhirType: {fhirValue.GetType().Name} for the SearchParameter entity with the database " +
                                $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                $"name of: {SearchParameterName}");
    }

    return SetPosition(position);
  }

  private IList<IndexPosition> SetPosition(Hl7.Fhir.Model.Location.PositionComponent position)
  {
    // Both latitude and longitude are 1..1 in the specification, but a resource can still
    // arrive without them, so absence is handled rather than assumed away.
    if (position.Latitude is null || position.Longitude is null)
    {
      return Array.Empty<IndexPosition>();
    }

    decimal latitude = position.Latitude.Value;
    decimal longitude = position.Longitude.Value;

    // FHIR types these as plain decimals with no range constraint, so neither the parser nor
    // profile validation rejects an impossible coordinate. SQL Server's geography type does
    // reject it. Skipping the index row rather than throwing keeps one malformed Location from
    // failing its own create or update; the resource stores normally, it is simply not findable
    // through the 'near' search parameter.
    if (latitude < MinimumLatitude || latitude > MaximumLatitude ||
        longitude < MinimumLongitude || longitude > MaximumLongitude)
    {
      logger.LogWarning(
        "A {ResourceType} resource had a position outside the valid WGS84 range and was not indexed for the " +
        "search parameter: {SearchParameterName} (database key {SearchParameterStoreId}). " +
        "Latitude was {Latitude} (valid range -90 to 90) and longitude was {Longitude} (valid range -180 to 180).",
        ResourceType.GetCode(), SearchParameterName, SearchParameterId.ToString(), latitude, longitude);

      return Array.Empty<IndexPosition>();
    }

    // NetTopologySuite orders a Point as (X, Y), which is (longitude, latitude).
    var point = new Point((double)longitude, (double)latitude) { SRID = Wgs84Srid };

    return new List<IndexPosition>
    {
      new IndexPosition(
        indexPositionId: null,
        resourceStoreId: null,
        resourceStore: null,
        searchParameterStoreId: SearchParameterId,
        searchParameterStore: null,
        position: point)
    };
  }
}
