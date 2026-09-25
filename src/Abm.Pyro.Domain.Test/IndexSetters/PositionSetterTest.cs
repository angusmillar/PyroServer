using System.Collections.Generic;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.IndexSetters;
using Abm.Pyro.Domain.Model;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abm.Pyro.Domain.Test.IndexSetters;

public class PositionSetterTest
{
    private const int SearchParameterId = 758;
    private const string SearchParameterName = "near";

    private static ITypedElement PositionElement(decimal? latitude, decimal? longitude)
    {
        var position = new Location.PositionComponent
        {
            LatitudeElement = latitude.HasValue ? new FhirDecimal(latitude.Value) : null,
            LongitudeElement = longitude.HasValue ? new FhirDecimal(longitude.Value) : null
        };

#pragma warning disable SDK0001 // ToTypedElement(Base, ModelInspector, string?) is marked experimental in Hl7.Fhir.Base
        return position.ToTypedElement(ModelInfo.ModelInspector);
#pragma warning restore SDK0001
    }

    private static PositionSetter CreateSut() =>
        new PositionSetter(NullLogger<PositionSetter>.Instance);

    [Fact]
    public void Set_ValidPosition_ReturnsSingleIndexRow()
    {
        IList<IndexPosition> result = CreateSut().Set(
            PositionElement(-33.8568m, 151.2153m),
            FhirResourceTypeId.Location, SearchParameterId, SearchParameterName);

        Assert.Single(result);
        Assert.Equal(SearchParameterId, result[0].SearchParameterStoreId);
        Assert.Equal(4326, result[0].Position.SRID);
    }

    /// <summary>
    /// NetTopologySuite's Point(x, y) is Point(longitude, latitude), the opposite of T-SQL's
    /// geography::Point(latitude, longitude, srid). Getting this backwards puts every Location
    /// somewhere else on earth without any error, so it is asserted explicitly.
    /// </summary>
    [Fact]
    public void Set_ValidPosition_PutsLongitudeInXAndLatitudeInY()
    {
        IList<IndexPosition> result = CreateSut().Set(
            PositionElement(-33.8568m, 151.2153m),
            FhirResourceTypeId.Location, SearchParameterId, SearchParameterName);

        Assert.Equal(151.2153d, result[0].Position.X, 6);
        Assert.Equal(-33.8568d, result[0].Position.Y, 6);
    }

    [Theory]
    [InlineData(null, 151.2153)]
    [InlineData(-33.8568, null)]
    [InlineData(null, null)]
    public void Set_MissingCoordinate_ReturnsNoRows(double? latitude, double? longitude)
    {
        IList<IndexPosition> result = CreateSut().Set(
            PositionElement((decimal?)latitude, (decimal?)longitude),
            FhirResourceTypeId.Location, SearchParameterId, SearchParameterName);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(200, 151.2153)]   // latitude above 90
    [InlineData(-91, 151.2153)]   // latitude below -90
    [InlineData(-33.8568, 181)]   // longitude above 180
    [InlineData(-33.8568, -181)]  // longitude below -180
    public void Set_OutOfRangeCoordinate_ReturnsNoRowsAndDoesNotThrow(decimal latitude, decimal longitude)
    {
        IList<IndexPosition> result = CreateSut().Set(
            PositionElement(latitude, longitude),
            FhirResourceTypeId.Location, SearchParameterId, SearchParameterName);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    [InlineData(0, 0)]
    public void Set_CoordinateAtTheLimit_ReturnsSingleIndexRow(decimal latitude, decimal longitude)
    {
        IList<IndexPosition> result = CreateSut().Set(
            PositionElement(latitude, longitude),
            FhirResourceTypeId.Location, SearchParameterId, SearchParameterName);

        Assert.Single(result);
    }
}
