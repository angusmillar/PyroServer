using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

/// <summary>
/// Searches on the <c>_lastUpdated</c> result parameter.
///
/// The server clock is frozen for the POST so the resource is committed with a known,
/// exact <c>Meta.LastUpdated</c>; the searches then sit either side of that instant.
/// </summary>
public class LastUpdatedSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    /// <summary>The instant the resource under test is committed at: 2026-09-25 10:29:00 UTC.</summary>
    private static readonly DateTimeOffset CommittedAtUtc =
        new(2026, 09, 25, 10, 29, 00, TimeSpan.Zero);

    /// <summary>
    /// The same wall-clock reading, but on a server whose configured default time zone is
    /// Australian Eastern Standard Time — as production runs. The stored UTC value is
    /// therefore 2026-09-25 00:29:00Z, ten hours behind the local reading.
    /// </summary>
    private static readonly DateTimeOffset CommittedAtAest =
        new(2026, 09, 25, 10, 29, 00, TimeSpan.FromHours(10));

    private async Task<Patient> CreatePatientAt(DateTimeOffset commitInstant)
    {
        Clock.FreezeAt(commitInstant);

        Patient? created = await FhirClient.CreateAsync(PatientBuilder.Build());

        Assert.NotNull(created);
        Assert.Equal(
            commitInstant.ToUniversalTime(),
            created.Meta!.LastUpdated!.Value.ToUniversalTime());

        return created;
    }

    private async Task<Bundle> Search(string lastUpdatedValue)
    {
        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { $"_lastUpdated={lastUpdatedValue}" });

        Assert.NotNull(bundle);
        return bundle;
    }

    // ---------------------------------------------------------------------
    // The resource is committed at 10:29 UTC. None of these windows contain it,
    // so every one of them must return an empty bundle.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("gt2026-09-25T10:30:00Z")]      // explicit UTC
    [InlineData("gt2026-09-25T10:30:00+00:00")] // explicit zero offset
    [InlineData("gt2026-09-26")]                // date-only, the day after
    [InlineData("lt2026-09-25T09:29:00Z")]      // an hour before the commit
    public async Task Search_ByLastUpdated_OutsideWindow_ReturnsEmptyBundle(string lastUpdatedValue)
    {
        await CreatePatientAt(CommittedAtUtc);

        Bundle bundle = await Search(lastUpdatedValue);

        Assert.Empty(bundle.Entry);
    }

    // ---------------------------------------------------------------------
    // Controls: the resource genuinely is inside these windows. If these pass while
    // the ones above fail, _lastUpdated is being dropped from the query entirely
    // rather than evaluated in the wrong direction.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("gt2026-09-25T09:29:00Z")]
    [InlineData("lt2026-09-25T11:29:00Z")]
    [InlineData("eq2026-09-25")]
    // A value with no time zone is read in the server's default time zone (+10:00, set by
    // ServiceDefaultTimeZone in appsettings.json), so "10:30" here means 00:30Z — which the
    // 10:29Z commit is genuinely after. See FhirDateTimeFactory.TryParseDateTimeToUniversalTime.
    [InlineData("gt2026-09-25T10:30:00")]
    public async Task Search_ByLastUpdated_InsideWindow_ReturnsResource(string lastUpdatedValue)
    {
        await CreatePatientAt(CommittedAtUtc);

        Bundle bundle = await Search(lastUpdatedValue);

        Assert.Single(bundle.Entry);
    }

    // ---------------------------------------------------------------------
    // The same scenario on a server running a non-UTC default time zone, which is
    // how production is configured. Committed at 10:29 AEST (00:29 UTC).
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("gt2026-09-25T10:30:00+10:00")] // a minute later, local reading
    [InlineData("gt2026-09-25T00:30:00Z")]      // the same instant, expressed as UTC
    [InlineData("gt2026-09-25T10:30:00")]       // as a client would naively type it
    public async Task Search_ByLastUpdated_NonUtcServerClock_OutsideWindow_ReturnsEmptyBundle(
        string lastUpdatedValue)
    {
        await CreatePatientAt(CommittedAtAest);

        Bundle bundle = await Search(lastUpdatedValue);

        Assert.Empty(bundle.Entry);
    }
}
