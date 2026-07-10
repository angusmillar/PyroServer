using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Chaining;

/// <summary>
/// Reverse chained search via _has: https://hl7.org/fhir/R4/search.html#has
/// </summary>
public class ReverseChainSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BodyWeightCode = "29463-7";
    private const string HeartRateCode = "8867-4";

    [Fact]
    public async Task Search_ByHasObservationCode_ReturnsMatchingPatient()
    {
        Patient patient = await CreatePatientAsync();
        await CreateObservationAsync(subjectPatientId: patient.Id, loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { $"_has:Observation:subject:code={CodeSystemUriSupport.Loinc}|{BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByHasObservationCode_NoMatch_ReturnsEmptyBundle()
    {
        Patient patient = await CreatePatientAsync();
        await CreateObservationAsync(subjectPatientId: patient.Id, loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { $"_has:Observation:subject:code={CodeSystemUriSupport.Loinc}|{HeartRateCode}" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByHasObservationCodeWithoutSystem_ReturnsMatchingPatient()
    {
        Patient patient = await CreatePatientAsync();
        await CreateObservationAsync(subjectPatientId: patient.Id, loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { $"_has:Observation:subject:code={BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByHasObservationCode_OnlyReturnsPatientWithMatchingObservation()
    {
        Patient patientWithObservation = await CreatePatientAsync(familyName: "Smith");
        await CreatePatientAsync(familyName: "Jones");
        await CreateObservationAsync(subjectPatientId: patientWithObservation.Id, loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { $"_has:Observation:subject:code={CodeSystemUriSupport.Loinc}|{BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
        Assert.IsType<Patient>(bundle.Entry.Single().Resource);
        if (bundle.Entry.Single().Resource is Patient patient)
        {
            Assert.Equal(patientWithObservation.Id, patient.Id);
        }
    }

    [Fact]
    public async Task Search_ByHasObservationCodeCombinedWithOwnFamilyParameter_ReturnsMatchingPatient()
    {
        Patient patient = await CreatePatientAsync(familyName: "Smith");
        await CreateObservationAsync(subjectPatientId: patient.Id, loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[]
            {
                "family=Smith",
                $"_has:Observation:subject:code={CodeSystemUriSupport.Loinc}|{BodyWeightCode}"
            });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    private async Task<Patient> CreatePatientAsync(string? familyName = null)
    {
        Patient? patient = await FhirClient.CreateAsync(PatientBuilder.Build(familyName: familyName));
        Assert.NotNull(patient);
        return patient;
    }

    private async Task CreateObservationAsync(string? subjectPatientId = null, string? loincCode = null)
    {
        Observation? observation = await FhirClient.CreateAsync(
            ObservationBuilder.Build(subjectPatientId: subjectPatientId, loincCode: loincCode));
        Assert.NotNull(observation);
    }
}
