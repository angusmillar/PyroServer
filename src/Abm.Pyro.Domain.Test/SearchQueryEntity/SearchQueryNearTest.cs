using System;
using System.Collections.Generic;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.SearchQueryEntity;
using Abm.Pyro.Domain.Support;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Domain.Test.SearchQueryEntity;

public class SearchQueryNearTest
{
    private static readonly LocationNearSettings Settings = new()
    {
        DefaultDistanceInMetres = 10_000,
        MaximumDistanceInMetres = 1_000_000,
        ReturnDistanceInSearchResults = true
    };

    private static SearchQueryNear CreateSut()
    {
        var searchParameter = new SearchParameterProjection(
            searchParameterStoreId: 758,
            code: "near",
            status: PublicationStatusId.Active,
            isCurrent: true,
            isDeleted: false,
            url: new Uri("http://hl7.org/fhir/SearchParameter/Location-near"),
            type: SearchParamType.Special,
            expression: "Location.position",
            multipleOr: null,
            multipleAnd: null,
            baseList: new List<SearchParameterStoreResourceTypeBase>(),
            targetList: new List<SearchParameterStoreResourceTypeTarget>(),
            comparatorList: new List<SearchParameterStoreComparator>(),
            modifierList: new List<SearchParameterStoreSearchModifierCode>(),
            componentList: new List<SearchParameterStoreComponent>());

        return new SearchQueryNear(searchParameter, FhirResourceTypeId.Location, "near=x", Settings);
    }

    [Fact]
    public async Task ParseValue_LatitudeLongitudeDistanceAndUnits_IsValid()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153|11.20|km");

        Assert.True(sut.IsValid);
        SearchQueryNearValue value = Assert.Single(sut.ValueList);
        Assert.Equal(-33.8568d, value.Latitude, 6);
        Assert.Equal(151.2153d, value.Longitude, 6);
        Assert.Equal(11200d, value.DistanceInMetres, 6);
        Assert.Equal(NearDistanceUnit.Kilometre, value.ReportUnit);
    }

    [Fact]
    public async Task ParseValue_UnitsOmitted_AssumesKilometres()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153|5");

        Assert.True(sut.IsValid);
        Assert.Equal(5000d, sut.ValueList[0].DistanceInMetres, 6);
        Assert.Equal(NearDistanceUnit.Kilometre, sut.ValueList[0].ReportUnit);
    }

    [Fact]
    public async Task ParseValue_EmptyUnitsSegment_AssumesKilometres()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153|5|");

        Assert.True(sut.IsValid);
        Assert.Equal(5000d, sut.ValueList[0].DistanceInMetres, 6);
        Assert.Equal(NearDistanceUnit.Kilometre, sut.ValueList[0].ReportUnit);
    }

    [Fact]
    public async Task ParseValue_EmptyDistanceSegmentWithExplicitUnit_UsesConfiguredDefault()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153||km");

        Assert.True(sut.IsValid);
        Assert.Equal(10_000d, sut.ValueList[0].DistanceInMetres, 6);
        Assert.Equal(NearDistanceUnit.Kilometre, sut.ValueList[0].ReportUnit);
    }

    [Fact]
    public async Task ParseValue_DistanceOmitted_UsesConfiguredDefault()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153");

        Assert.True(sut.IsValid);
        Assert.Equal(10_000d, sut.ValueList[0].DistanceInMetres, 6);
    }

    [Fact]
    public async Task ParseValue_Miles_ConvertsToMetres()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153|1|mi");

        Assert.True(sut.IsValid);
        Assert.Equal(1609.344d, sut.ValueList[0].DistanceInMetres, 6);
        Assert.Equal(NearDistanceUnit.Mile, sut.ValueList[0].ReportUnit);
    }

    [Fact]
    public async Task ParseValue_MultiplePositionsSeparatedByComma_ProducesTwoValues()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33.8568|151.2153|5|km,-37.8183|144.9671|5|km");

        Assert.True(sut.IsValid);
        Assert.Equal(2, sut.ValueList.Count);
        Assert.Equal(-33.8568d, sut.ValueList[0].Latitude, 6);
        Assert.Equal(-37.8183d, sut.ValueList[1].Latitude, 6);
    }

    /// <summary>
    /// Review Focus 1. A decimal written with a comma collides with the OR delimiter. It must
    /// be rejected, never silently split into two malformed terms and half-parsed. Rejected
    /// because term 1 ("-33") collapses to a single vertical-bar segment once the OR delimiter
    /// has split the string, not because commas are detected as decimal separators — see
    /// <see cref="ParseValue_CommaThatCouldBeADecimalButFormsValidTerms_ParsesAsTwoPositions"/>
    /// for the case where the same digit-comma-digit shape forms two valid terms instead.
    /// </summary>
    [Fact]
    public async Task ParseValue_CommaDecimalProducingAMalformedTerm_IsInvalid()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-33,8568|151,2153|5|km");

        Assert.False(sut.IsValid);
        Assert.NotNull(sut.InvalidMessage);
    }

    /// <summary>
    /// A comma is the OR separator in FHIR R4 near values and FHIR decimals always use a full
    /// stop, so "-10.5|20,5|7" genuinely denotes TWO positions. A client that typed a comma as a
    /// decimal separator cannot be detected here: a legitimate multi-position search such as
    /// "33.8|151.2|5,37.8|144.9|5" contains the same digit-comma-digit sequence. Rejecting one
    /// would reject the other. This test pins the grammar-correct reading so the behaviour is
    /// deliberate rather than accidental.
    /// </summary>
    [Fact]
    public async Task ParseValue_CommaThatCouldBeADecimalButFormsValidTerms_ParsesAsTwoPositions()
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue("-10.5|20,5|7");

        Assert.True(sut.IsValid);
        Assert.Equal(2, sut.ValueList.Count);
        Assert.Equal(-10.5d, sut.ValueList[0].Latitude, 6);
        Assert.Equal(20d, sut.ValueList[0].Longitude, 6);
        Assert.Equal(5d, sut.ValueList[1].Latitude, 6);
        Assert.Equal(7d, sut.ValueList[1].Longitude, 6);
    }

    /// <summary>Review Focus 4. Zero and negative radii are meaningless and must be rejected.</summary>
    [Theory]
    [InlineData("-33.8568|151.2153|0|km")]
    [InlineData("-33.8568|151.2153|-5|km")]
    public async Task ParseValue_ZeroOrNegativeDistance_IsInvalid(string value)
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue(value);

        Assert.False(sut.IsValid);
    }

    [Theory]
    [InlineData("91|151.2153|5|km")]
    [InlineData("-91|151.2153|5|km")]
    [InlineData("-33.8568|181|5|km")]
    [InlineData("-33.8568|-181|5|km")]
    public async Task ParseValue_OutOfRangeCoordinate_IsInvalid(string value)
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue(value);

        Assert.False(sut.IsValid);
    }

    [Theory]
    [InlineData("-33.8568")]                       // too few segments
    [InlineData("-33.8568|151.2153|5|km|extra")]   // too many segments
    [InlineData("|151.2153|5|km")]                 // empty latitude
    [InlineData("-33.8568||5|km")]                 // empty longitude
    [InlineData("abc|151.2153|5|km")]              // non-numeric latitude
    [InlineData("-33.8568|151.2153|abc|km")]       // non-numeric distance
    public async Task ParseValue_MalformedValue_IsInvalid(string value)
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue(value);

        Assert.False(sut.IsValid);
    }

    [Theory]
    [InlineData("-33.8568|151.2153|5|cm")]
    [InlineData("-33.8568|151.2153|5|ft")]
    [InlineData("-33.8568|151.2153|5|nmi")]
    public async Task ParseValue_UnsupportedUnit_IsInvalid(string value)
    {
        SearchQueryNear sut = CreateSut();

        await sut.ParseValue(value);

        Assert.False(sut.IsValid);
        Assert.Contains("Supported units", sut.InvalidMessage);
    }

    [Fact]
    public async Task ParseValue_DistanceAboveConfiguredMaximum_IsInvalid()
    {
        SearchQueryNear sut = CreateSut();

        // 2000 km, above the 1,000,000 metre maximum
        await sut.ParseValue("-33.8568|151.2153|2000|km");

        Assert.False(sut.IsValid);
    }

    [Fact]
    public async Task ParseValue_DistanceJustUnderConfiguredMaximum_IsValid()
    {
        SearchQueryNear sut = CreateSut();

        // 999 km, just under the 1,000,000 metre maximum
        await sut.ParseValue("-33.8568|151.2153|999|km");

        Assert.True(sut.IsValid);
        Assert.Equal(999_000d, sut.ValueList[0].DistanceInMetres, 6);
    }

    [Fact]
    public async Task ParseValue_MissingModifierTrue_ProducesMissingValue()
    {
        SearchQueryNear sut = CreateSut();
        sut.Modifier = SearchModifierCodeId.Missing;

        await sut.ParseValue("true");

        Assert.True(sut.IsValid);
        Assert.True(Assert.Single(sut.ValueList).IsMissing);
    }

    [Fact]
    public async Task ParseValue_MissingModifierNotBoolean_IsInvalid()
    {
        SearchQueryNear sut = CreateSut();
        sut.Modifier = SearchModifierCodeId.Missing;

        await sut.ParseValue("banana");

        Assert.False(sut.IsValid);
    }
}
