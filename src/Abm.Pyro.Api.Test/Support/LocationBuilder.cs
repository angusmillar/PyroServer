using Hl7.Fhir.Model;

namespace Abm.Pyro.Api.Test.Support;

public static class LocationBuilder
{
    public static Location Build(
        string? id = null,
        string? name = null,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        var location = new Location
        {
            Id = id,
            Name = name ?? "TestLocation"
        };

        if (latitude.HasValue && longitude.HasValue)
        {
            location.Position = new Location.PositionComponent
            {
                LatitudeElement = new FhirDecimal(latitude.Value),
                LongitudeElement = new FhirDecimal(longitude.Value)
            };
        }

        return location;
    }
}
