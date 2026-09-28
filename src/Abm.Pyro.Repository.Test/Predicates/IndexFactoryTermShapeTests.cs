using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.SearchQueryEntity;
using Abm.Pyro.Repository.Predicates;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Repository.Test.Predicates;

/// <summary>
/// Pins each factory's ':missing' / ':not' term shape by evaluating the returned ResourceStore
/// predicate against in-memory ResourceStore instances. Asserting behaviour rather than expression
/// tree structure means a later refactor that preserves the semantics will not break these.
/// </summary>
public class IndexFactoryTermShapeTests
{
    private const int SearchParameterId = 42;
    private const int OtherSearchParameterId = 99;

    /// <summary>
    /// Mirrors the projection-building pattern already used by
    /// Abm.Pyro.Domain.Test/SearchQueryEntity/SearchQueryNearTest.cs.
    /// </summary>
    private static SearchParameterProjection Projection(string code, SearchParamType type) =>
        new(
            searchParameterStoreId: SearchParameterId,
            code: code,
            status: PublicationStatusId.Active,
            isCurrent: true,
            isDeleted: false,
            url: new Uri($"http://hl7.org/fhir/SearchParameter/{code}"),
            type: type,
            expression: null,
            multipleOr: null,
            multipleAnd: null,
            baseList: [],
            targetList: [],
            comparatorList: [],
            modifierList: [],
            componentList: []);

    private static ResourceStore Store(
        List<IndexString>? strings = null,
        List<IndexToken>? tokens = null,
        List<IndexUri>? uris = null,
        List<IndexDateTime>? dateTimes = null,
        List<IndexQuantity>? quantities = null,
        List<IndexReference>? references = null) =>
        new(
            resourceStoreId: 1,
            resourceId: "one",
            versionId: 1,
            resourceType: FhirResourceTypeId.Patient,
            isCurrent: true,
            isDeleted: false,
            httpVerb: HttpVerbId.Post,
            json: "{}",
            lastUpdatedUtc: DateTime.UtcNow,
            indexReferenceList: references ?? [],
            indexStringList: strings ?? [],
            indexDateTimeList: dateTimes ?? [],
            indexQuantityList: quantities ?? [],
            indexTokenList: tokens ?? [],
            indexUriList: uris ?? [],
            rowVersion: 1);

    private static Func<ResourceStore, bool> Compile(Expression<Func<ResourceStore, bool>> predicate) =>
        predicate.Compile();

    private static async Task<TQuery> MissingQuery<TQuery>(TQuery query, bool isMissing)
        where TQuery : SearchQueryBase
    {
        query.Modifier = SearchModifierCodeId.Missing;
        await query.ParseValue(isMissing.ToString().ToLowerInvariant());
        Assert.True(query.IsValid, query.InvalidMessage);
        return query;
    }

    // ---- IndexString ---------------------------------------------------------------------

    [Fact]
    public async Task StringIndex_MissingTrue_MatchesStoreWithNoRowForThatParameter()
    {
        var factory = new IndexStringPredicateFactory();
        SearchQueryString query = await MissingQuery(
            new SearchQueryString(Projection("name", SearchParamType.String), FhirResourceTypeId.Patient, "name:missing=true"),
            isMissing: true);

        Func<ResourceStore, bool> matches = Compile(factory.StringIndex(query));

        Assert.True(matches(Store()));

        // A row for a DIFFERENT search parameter is still absence for THIS one. The old
        // 'spid <> @x' idiom got exactly this case wrong.
        Assert.True(matches(Store(strings: [new IndexString(1, 1, null, OtherSearchParameterId, null, "smith")])));

        Assert.False(matches(Store(strings: [new IndexString(1, 1, null, SearchParameterId, null, "smith")])));
    }

    [Fact]
    public async Task StringIndex_MissingFalse_MatchesOnlyStoreWithARowForThatParameter()
    {
        var factory = new IndexStringPredicateFactory();
        SearchQueryString query = await MissingQuery(
            new SearchQueryString(Projection("name", SearchParamType.String), FhirResourceTypeId.Patient, "name:missing=false"),
            isMissing: false);

        Func<ResourceStore, bool> matches = Compile(factory.StringIndex(query));

        Assert.False(matches(Store()));
        Assert.False(matches(Store(strings: [new IndexString(1, 1, null, OtherSearchParameterId, null, "smith")])));
        Assert.True(matches(Store(strings: [new IndexString(1, 1, null, SearchParameterId, null, "smith")])));
    }

    // ---- IndexUri -----------------------------------------------------------------------

    [Fact]
    public async Task UriIndex_MissingTrue_MatchesStoreWithNoRowForThatParameter()
    {
        var factory = new IndexUriPredicateFactory();
        SearchQueryUri query = await MissingQuery(
            new SearchQueryUri(Projection("url", SearchParamType.Uri), FhirResourceTypeId.ValueSet, "url:missing=true"),
            isMissing: true);

        Func<ResourceStore, bool> matches = Compile(factory.UriIndex(query));

        Assert.True(matches(Store()));
        Assert.True(matches(Store(uris: [new IndexUri(1, 1, null, OtherSearchParameterId, null, "http://x")])));
        Assert.False(matches(Store(uris: [new IndexUri(1, 1, null, SearchParameterId, null, "http://x")])));
    }

    [Fact]
    public async Task UriIndex_MissingFalse_MatchesOnlyStoreWithARowForThatParameter()
    {
        var factory = new IndexUriPredicateFactory();
        SearchQueryUri query = await MissingQuery(
            new SearchQueryUri(Projection("url", SearchParamType.Uri), FhirResourceTypeId.ValueSet, "url:missing=false"),
            isMissing: false);

        Func<ResourceStore, bool> matches = Compile(factory.UriIndex(query));

        Assert.False(matches(Store()));
        Assert.True(matches(Store(uris: [new IndexUri(1, 1, null, SearchParameterId, null, "http://x")])));
    }

    // ---- IndexToken ---------------------------------------------------------------------

    [Fact]
    public async Task TokenIndex_MissingTrue_MatchesStoreWithNoRowForThatParameter()
    {
        var factory = new IndexTokenPredicateFactory();
        SearchQueryToken query = await MissingQuery(
            new SearchQueryToken(Projection("gender", SearchParamType.Token), FhirResourceTypeId.Patient, "gender:missing=true"),
            isMissing: true);

        Func<ResourceStore, bool> matches = Compile(factory.TokenIndex(query));

        Assert.True(matches(Store()));
        Assert.True(matches(Store(tokens: [new IndexToken(1, 1, null, OtherSearchParameterId, null, "male", null)])));
        Assert.False(matches(Store(tokens: [new IndexToken(1, 1, null, SearchParameterId, null, "male", null)])));
    }

    [Fact]
    public async Task TokenIndex_MissingFalse_MatchesRowWithNullSystem()
    {
        // Presence is presence: a row whose System is null still counts as having the parameter.
        var factory = new IndexTokenPredicateFactory();
        SearchQueryToken query = await MissingQuery(
            new SearchQueryToken(Projection("gender", SearchParamType.Token), FhirResourceTypeId.Patient, "gender:missing=false"),
            isMissing: false);

        Func<ResourceStore, bool> matches = Compile(factory.TokenIndex(query));

        Assert.False(matches(Store()));
        Assert.True(matches(Store(tokens: [new IndexToken(1, 1, null, SearchParameterId, null, "male", null)])));
    }

    [Fact]
    public async Task TokenIndex_NotWithTwoValues_ExcludesStoreHavingEitherValue()
    {
        // De Morgan: ':not=male,female' folds with And, so a store with EITHER value is excluded.
        // Read as a literal OR of negations this query would match everything.
        var factory = new IndexTokenPredicateFactory();
        var query = new SearchQueryToken(
            Projection("gender", SearchParamType.Token), FhirResourceTypeId.Patient, "gender:not=male,female")
        {
            Modifier = SearchModifierCodeId.Not
        };
        await query.ParseValue("male,female");
        Assert.True(query.IsValid, query.InvalidMessage);

        Func<ResourceStore, bool> matches = Compile(factory.TokenIndex(query));

        Assert.False(matches(Store(tokens: [new IndexToken(1, 1, null, SearchParameterId, null, "male", null)])));
        Assert.False(matches(Store(tokens: [new IndexToken(1, 1, null, SearchParameterId, null, "female", null)])));
        Assert.True(matches(Store(tokens: [new IndexToken(1, 1, null, SearchParameterId, null, "other", null)])));

        // FHIR R4: ':not' includes resources with no value for the parameter.
        Assert.True(matches(Store()));
    }

    [Fact]
    public async Task TokenIndex_NotWithSiblingRow_ExcludesStoreCarryingTheValue()
    {
        // The false-positive case at the unit level: two rows for the same parameter, one of which
        // matches. Inverting inside the row test let the non-matching sibling satisfy it.
        var factory = new IndexTokenPredicateFactory();
        var query = new SearchQueryToken(
            Projection("code", SearchParamType.Token), FhirResourceTypeId.Observation, "code:not=male")
        {
            Modifier = SearchModifierCodeId.Not
        };
        await query.ParseValue("male");
        Assert.True(query.IsValid, query.InvalidMessage);

        Func<ResourceStore, bool> matches = Compile(factory.TokenIndex(query));

        Assert.False(matches(Store(tokens:
        [
            new IndexToken(1, 1, null, SearchParameterId, null, "male", null),
            new IndexToken(2, 1, null, SearchParameterId, null, "other", null)
        ])));
    }

    // ---- IndexDateTime ------------------------------------------------------------------

    [Fact]
    public async Task DateTimeIndex_MissingTrue_MatchesStoreWithNoRowForThatParameter()
    {
        var factory = new IndexDateTimePredicateFactory(new Abm.Pyro.Domain.FhirSupport.FhirDateTimeSupport());

        // ':missing' never parses a date value, so the date factory is never exercised here.
        var dateTimeFactory = new Mock<Abm.Pyro.Domain.FhirSupport.IFhirDateTimeFactory>();

        SearchQueryDateTime query = await MissingQuery(
            new SearchQueryDateTime(
                Projection("birthdate", SearchParamType.Date),
                FhirResourceTypeId.Patient,
                "birthdate:missing=true",
                dateTimeFactory.Object),
            isMissing: true);

        Func<ResourceStore, bool> matches = Compile(factory.DateTimeIndex(query));

        Assert.True(matches(Store()));
        Assert.True(matches(Store(dateTimes: [new IndexDateTime(1, 1, null, OtherSearchParameterId, null, DateTime.UtcNow, DateTime.UtcNow)])));
        Assert.False(matches(Store(dateTimes: [new IndexDateTime(1, 1, null, SearchParameterId, null, DateTime.UtcNow, DateTime.UtcNow)])));
    }

    // ---- IndexQuantity / IndexNumber ----------------------------------------------------

    [Fact]
    public async Task QuantityIndex_MissingTrue_MatchesStoreWithNoRowForThatParameter()
    {
        var factory = new IndexQuantityPredicateFactory();
        SearchQueryQuantity query = await MissingQuery(
            new SearchQueryQuantity(Projection("value-quantity", SearchParamType.Quantity), FhirResourceTypeId.Observation, "value-quantity:missing=true"),
            isMissing: true);

        Func<ResourceStore, bool> matches = Compile(factory.QuantityIndex(query));

        Assert.True(matches(Store()));
        Assert.False(matches(Store(quantities: [Quantity(SearchParameterId)])));
        Assert.True(matches(Store(quantities: [Quantity(OtherSearchParameterId)])));
    }

    [Fact]
    public async Task NumberIndex_MissingTrue_MatchesStoreWithNoRowForThatParameter()
    {
        var factory = new IndexNumberPredicateFactory();
        SearchQueryNumber query = await MissingQuery(
            new SearchQueryNumber(Projection("probability", SearchParamType.Number), FhirResourceTypeId.RiskAssessment, "probability:missing=true"),
            isMissing: true);

        Func<ResourceStore, bool> matches = Compile(factory.NumberIndex(query));

        Assert.True(matches(Store()));
        Assert.False(matches(Store(quantities: [Quantity(SearchParameterId)])));
        Assert.True(matches(Store(quantities: [Quantity(OtherSearchParameterId)])));
    }

    private static IndexQuantity Quantity(int searchParameterStoreId) =>
        new(1, 1, null, searchParameterStoreId, null, null, 1m, "kg", "http://unitsofmeasure.org", "kg", null, null, null, null, null);
}
