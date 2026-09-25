using Abm.Pyro.Domain.Support;
using Xunit;

namespace Abm.Pyro.Domain.Test.Support;

public class NearDistanceUnitSupportTest
{
    [Theory]
    [InlineData("km", NearDistanceUnit.Kilometre)]
    [InlineData("KM", NearDistanceUnit.Kilometre)]
    [InlineData("m", NearDistanceUnit.Metre)]
    [InlineData("[mi_i]", NearDistanceUnit.Mile)]
    [InlineData("mi", NearDistanceUnit.Mile)]
    [InlineData("mile", NearDistanceUnit.Mile)]
    [InlineData("miles", NearDistanceUnit.Mile)]
    [InlineData("", NearDistanceUnit.Kilometre)]
    [InlineData(null, NearDistanceUnit.Kilometre)]
    public void TryParse_SupportedUnit_ReturnsTrueAndExpectedUnit(string? unitCode, NearDistanceUnit expected)
    {
        bool result = NearDistanceUnitSupport.TryParse(unitCode, out NearDistanceUnit unit);

        Assert.True(result);
        Assert.Equal(expected, unit);
    }

    [Theory]
    [InlineData("cm")]
    [InlineData("ft")]
    [InlineData("[ft_i]")]
    [InlineData("in")]
    [InlineData("nmi")]
    [InlineData("[nmi_i]")]
    [InlineData("furlong")]
    public void TryParse_UnsupportedUnit_ReturnsFalse(string unitCode)
    {
        bool result = NearDistanceUnitSupport.TryParse(unitCode, out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData(1, NearDistanceUnit.Metre, 1d)]
    [InlineData(1, NearDistanceUnit.Kilometre, 1000d)]
    [InlineData(1, NearDistanceUnit.Mile, 1609.344d)]
    [InlineData(11.2, NearDistanceUnit.Kilometre, 11200d)]
    public void ToMetres_ConvertsCorrectly(decimal distance, NearDistanceUnit unit, double expectedMetres)
    {
        double result = NearDistanceUnitSupport.ToMetres(distance, unit);

        Assert.Equal(expectedMetres, result, 6);
    }

    [Theory]
    [InlineData(1609.344d, NearDistanceUnit.Mile, 1d)]
    [InlineData(1000d, NearDistanceUnit.Kilometre, 1d)]
    [InlineData(2500d, NearDistanceUnit.Metre, 2500d)]
    public void FromMetres_IsTheInverseOfToMetres(double metres, NearDistanceUnit unit, double expected)
    {
        double result = NearDistanceUnitSupport.FromMetres(metres, unit);

        Assert.Equal(expected, result, 6);
    }

    [Theory]
    [InlineData(NearDistanceUnit.Metre, "m", "m")]
    [InlineData(NearDistanceUnit.Kilometre, "km", "km")]
    [InlineData(NearDistanceUnit.Mile, "[mi_i]", "mi")]
    public void UcumCodeAndDisplayUnit_AreCorrect(NearDistanceUnit unit, string expectedCode, string expectedDisplay)
    {
        Assert.Equal(expectedCode, NearDistanceUnitSupport.UcumCode(unit));
        Assert.Equal(expectedDisplay, NearDistanceUnitSupport.DisplayUnit(unit));
    }
}
