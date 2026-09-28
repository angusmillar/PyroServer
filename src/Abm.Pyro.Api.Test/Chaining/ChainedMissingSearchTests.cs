using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Chaining;

/// <summary>
/// The chained and _has forms of ':missing'. Neither path has its own negation handling: both
/// delegate their terminal node to ISearchPredicateFactory.GetResourceStoreIndexPredicate, so they
/// inherit the fix made in the index predicate factories. These tests exist because inheritance by
/// delegation is a claim until it is proven.
/// </summary>
public class ChainedMissingSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Search_ChainedOrganizationNameMissing_ReturnsPatientWhoseOrganizationHasNoName()
    {
        Organization? named = await FhirClient.CreateAsync(OrganizationBuilder.Build(name: "Acme Health"));
        Organization? unnamed = await FhirClient.CreateAsync(OrganizationBuilder.Build(includeName: false));
        Assert.NotNull(named);
        Assert.NotNull(unnamed);

        Patient? patientOfNamed = await FhirClient.CreateAsync(
            PatientBuilder.Build(managingOrganizationId: named.Id));
        Patient? patientOfUnnamed = await FhirClient.CreateAsync(
            PatientBuilder.Build(managingOrganizationId: unnamed.Id));
        Assert.NotNull(patientOfNamed);
        Assert.NotNull(patientOfUnnamed);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "organization.name:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([patientOfUnnamed.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    [Fact]
    public async Task Search_ChainedOrganizationNameMissingFalse_ReturnsPatientWhoseOrganizationIsNamed()
    {
        Organization? named = await FhirClient.CreateAsync(OrganizationBuilder.Build(name: "Acme Health"));
        Organization? unnamed = await FhirClient.CreateAsync(OrganizationBuilder.Build(includeName: false));
        Assert.NotNull(named);
        Assert.NotNull(unnamed);

        Patient? patientOfNamed = await FhirClient.CreateAsync(
            PatientBuilder.Build(managingOrganizationId: named.Id));
        Patient? patientOfUnnamed = await FhirClient.CreateAsync(
            PatientBuilder.Build(managingOrganizationId: unnamed.Id));
        Assert.NotNull(patientOfNamed);
        Assert.NotNull(patientOfUnnamed);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "organization.name:missing=false" });

        Assert.NotNull(bundle);
        Assert.Equal([patientOfNamed.Id], bundle.Entry.Select(e => e.Resource.Id).Order());
    }

    /// <summary>
    /// ':missing' on a _has terminal parameter is currently REJECTED with a 400, and this test pins
    /// that as the present behaviour rather than endorsing it.
    ///
    /// The cause is the _has grammar parser, not negation: _has splits its segments on ':', so
    /// '_has:Observation:subject:value-quantity:missing=true' has one segment more than the parser
    /// accepts and it never reaches the predicate layer at all. Adding modifier support to the _has
    /// parser is out of scope for the negation work — see the design doc's "Out of scope".
    ///
    /// If _has modifier parsing is ever implemented, this test will start failing. That is the
    /// signal to convert it into a positive assertion like the chained tests above.
    /// </summary>
    [Fact]
    public async Task Search_HasWithMissingModifier_IsCurrentlyRejectedByTheHasParser()
    {
        Patient? patient = await FhirClient.CreateAsync(PatientBuilder.Build());
        Assert.NotNull(patient);

        Hl7.Fhir.Rest.FhirOperationException exception =
            await Assert.ThrowsAsync<Hl7.Fhir.Rest.FhirOperationException>(
                () => FhirClient.SearchAsync<Patient>(
                    new[] { "_has:Observation:subject:value-quantity:missing=true" }));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.Status);
    }
}
