using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

/// <summary>
/// Exercises FHIR R4 number search prefixes (eq, ne, gt, lt, ge, le) via the
/// RiskAssessment.probability search parameter (type: number).
/// Spec: https://hl7.org/fhir/R4/search.html#number
/// </summary>
public class NumberSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    // --- eq ---

    [Fact]
    public async Task Search_EqPrefix_MatchesStoredValue()
    {
        // eq: the stored value equals the search value (within implicit range of the given precision).
        await CreateAsync(probability: 0.8M);

        Bundle? bundle = await FhirClient.SearchAsync<RiskAssessment>(
            new[] { "probability=eq0.8" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_NoPrefix_DefaultsToEq_MatchesStoredValue()
    {
        // When no prefix is supplied the server treats it as eq.
        await CreateAsync(probability: 0.8M);

        Bundle? bundle = await FhirClient.SearchAsync<RiskAssessment>(
            new[] { "probability=0.8" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    // --- ne ---

    [Fact]
    public async Task Search_NePrefix_ExcludesEqualResource_ReturnsEmptyBundle()
    {
        // ne: the stored value is NOT equal to the search value — equal resource must not appear.
        await CreateAsync(probability: 0.8M);

        Bundle? bundle = await FhirClient.SearchAsync<RiskAssessment>(
            new[] { "probability=ne0.8" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_NePrefix_ReturnsNonEqualResources()
    {
        // ne: a resource with a different value must still appear in results.
        await CreateAsync(probability: 0.3M);
        await CreateAsync(probability: 0.8M);

        Bundle? bundle = await FhirClient.SearchAsync<RiskAssessment>(
            new[] { "probability=ne0.8" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    // --- gt ---

    [Fact]
    public async Task Search_GtPrefix_ReturnsResourceAboveThreshold()
    {
        // gt: stored 0.8 is greater than search value 0.7 — should match.
        await CreateAsync(probability: 0.8M);

        Bundle? bundle = await FhirClient.SearchAsync<RiskAssessment>(
            new[] { "probability=gt0.7" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_GtPrefix_ExcludesEqualValue_ReturnsEmptyBundle()
    {
        // gt is strictly greater-than — an equal value must not match.
        await CreateAsync(probability: 0.8M);

        Bundle? bundle = await FhirClient.SearchAsync<RiskAssessment>(
            new[] { "probability=gt0.8" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    // --- lt ---

    [Fact]
    public async Task Search_LtPrefix_ReturnsResourceBelowThreshold()
    {
        // lt: stored 0.3 is less than search value 0.5 — should match.
        await CreateAsync(probability: 0.3M);

        Bundle? bundle = await FhirClient.SearchAsync<RiskAssessment>(
            new[] { "probability=lt0.5" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_LtPrefix_ExcludesEqualValue_ReturnsEmptyBundle()
    {
        // lt is strictly less-than — an equal value must not match.
        await CreateAsync(probability: 0.3M);

        Bundle? bundle = await FhirClient.SearchAsync<RiskAssessment>(
            new[] { "probability=lt0.3" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    // --- ge ---

    [Fact]
    public async Task Search_GePrefix_IncludesEqualValue()
    {
        // ge: stored 0.8 >= search value 0.8 — boundary value must match.
        await CreateAsync(probability: 0.8M);

        Bundle? bundle = await FhirClient.SearchAsync<RiskAssessment>(
            new[] { "probability=ge0.8" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    // --- le ---

    [Fact]
    public async Task Search_LePrefix_IncludesEqualValue()
    {
        // le: stored 0.3 <= search value 0.3 — boundary value must match.
        await CreateAsync(probability: 0.3M);

        Bundle? bundle = await FhirClient.SearchAsync<RiskAssessment>(
            new[] { "probability=le0.3" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    private async Task CreateAsync(decimal probability)
    {
        Patient? patient = await FhirClient.CreateAsync(PatientBuilder.Build());
        Assert.NotNull(patient);

        RiskAssessment? created = await FhirClient.CreateAsync(
            RiskAssessmentBuilder.Build(probability: probability, subjectPatientId: patient.Id));
        Assert.NotNull(created);
    }
}
