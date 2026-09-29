using Abm.Pyro.Api.Test.Fixtures;
using Abm.Pyro.Api.Test.Support;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Test.Search;

public class TokenIndexSearchTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BodyWeightCode = "29463-7";
    private const string HeartRateCode = "8867-4";
    private const string SnomedBodyWeightCode = "27113001";

    [Fact]
    public async Task Search_ByCode_ReturnsMatchingObservation()
    {
        await CreateObservationAsync(loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code={CodeSystemUriSupport.Loinc}|{BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByCode_NoMatch_ReturnsEmptyBundle()
    {
        await CreateObservationAsync(loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code={CodeSystemUriSupport.Loinc}|{HeartRateCode}" });

        Assert.NotNull(bundle);
        Assert.Empty(bundle.Entry);
    }

    [Fact]
    public async Task Search_ByCodeWithoutSystem_ReturnsMatchingObservation()
    {
        // FHIR token search with no system prefix matches on code alone.
        await CreateObservationAsync(loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code={BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }
    
    [Fact]
    public async Task Search_BySystemWithoutCode_ReturnsMatchingObservation()
    {
        // FHIR token search with no code matches on system alone.
        await CreateObservationAsync(loincCode: BodyWeightCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code={CodeSystemUriSupport.Loinc}|" });

        Assert.NotNull(bundle);
        Assert.Single(bundle.Entry);
    }
    
    [Fact]
    public async Task Search_BySystemOnly_ReturnsMatchingObservations()
    {
        // FHIR token search with no system prefix matches on code alone.
        await CreateObservationAsync(loincCode: BodyWeightCode);
        await CreateObservationAsync(loincCode: HeartRateCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code={CodeSystemUriSupport.Loinc}|" });

        Assert.NotNull(bundle);
        Assert.Equal(2, bundle.Entry.Count);
    }

    [Fact]
    public async Task Search_GenderMissingTrue_ReturnsOnlyPatientWithoutGender()
    {
        // Patient.gender rather than Observation.code: Observation.code has minimum cardinality 1
        // in FHIR R4, so a codeless Observation cannot be created at all.
        Patient? withGender = await FhirClient.CreateAsync(PatientBuilder.Build());
        Patient? withoutGender = await FhirClient.CreateAsync(PatientBuilder.Build(includeGender: false));
        Assert.NotNull(withGender);
        Assert.NotNull(withoutGender);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "gender:missing=true" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutGender.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_GenderMissingFalse_ReturnsOnlyPatientWithGender()
    {
        Patient? withGender = await FhirClient.CreateAsync(PatientBuilder.Build());
        Patient? withoutGender = await FhirClient.CreateAsync(PatientBuilder.Build(includeGender: false));
        Assert.NotNull(withGender);
        Assert.NotNull(withoutGender);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "gender:missing=false" });

        Assert.NotNull(bundle);
        Assert.Equal([withGender.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_GenderNot_IncludesPatientWithNoGender()
    {
        // FHIR R4: ':not' includes resources that have no value for the parameter.
        Patient? unknownGender = await FhirClient.CreateAsync(PatientBuilder.Build());
        Patient? noGender = await FhirClient.CreateAsync(PatientBuilder.Build(includeGender: false));
        Assert.NotNull(unknownGender);
        Assert.NotNull(noGender);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(new[] { "gender:not=unknown" });

        Assert.NotNull(bundle);
        Assert.Equal([noGender.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_CodeNot_ExcludesObservationWithSiblingCoding()
    {
        // The false-positive case. The old predicate inverted the comparison INSIDE the EXISTS,
        // so the snomed coding satisfied 'System <> loinc' and the Observation wrongly matched
        // despite genuinely carrying loinc|29463-7.
        Observation? twoCodings = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: BodyWeightCode, snomedCode: SnomedBodyWeightCode));
        Observation? otherCode = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: HeartRateCode));
        Assert.NotNull(twoCodings);
        Assert.NotNull(otherCode);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code:not={CodeSystemUriSupport.Loinc}|{BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Equal([otherCode.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_CodeNotMultipleValues_ExcludesBoth()
    {
        // De Morgan: negating an OR'd value set is an AND of negations. Read as a literal OR
        // this query would match everything.
        Observation? bodyWeight = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: BodyWeightCode));
        Observation? heartRate = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: HeartRateCode));
        Observation? other = await FhirClient.CreateAsync(ObservationBuilder.Build(loincCode: "1234-5"));
        Assert.NotNull(bodyWeight);
        Assert.NotNull(heartRate);
        Assert.NotNull(other);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code:not={BodyWeightCode},{HeartRateCode}" });

        Assert.NotNull(bundle);
        Assert.Equal([other.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_CodeNotSystemOnly_ExcludesEveryObservationInThatSystem()
    {
        // Review Focus 4: the MatchSystemOnly form under negation. The comparison resource carries
        // a SNOMED-only coding rather than no code at all, because Observation.code is 1..1.
        var snomedOnly = ObservationBuilder.Build();
        snomedOnly.Code = new CodeableConcept
        {
            Coding = [new Coding { System = CodeSystemUriSupport.Snomed, Code = SnomedBodyWeightCode }]
        };

        Observation? loincObservation = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: BodyWeightCode));
        Observation? snomedObservation = await FhirClient.CreateAsync(snomedOnly);
        Assert.NotNull(loincObservation);
        Assert.NotNull(snomedObservation);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code:not={CodeSystemUriSupport.Loinc}|" });

        Assert.NotNull(bundle);
        Assert.Equal([snomedObservation.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_CodeNotWithEmptySystemPrefix_MatchesCodingsThatCarryASystem()
    {
        // Spec section 5.1's third ':not' case. The old hand-inverted predicate was
        // 'System == null && Code != c', which required a null-system row to EXIST, so an
        // Observation whose codings all carry a system was wrongly omitted.
        var noSystem = ObservationBuilder.Build();
        noSystem.Code = new CodeableConcept
        {
            Coding = [new Coding { Code = BodyWeightCode }] // no System
        };

        Observation? withoutSystem = await FhirClient.CreateAsync(noSystem);
        Observation? withSystem = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: BodyWeightCode));
        Assert.NotNull(withoutSystem);
        Assert.NotNull(withSystem);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code:not=|{BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Equal([withSystem.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_TwoNegatedParameters_CombineWithAnd()
    {
        // Review Focus 3: negations must AND across parameters, not just within one.
        Patient? noNameNoGender = await FhirClient.CreateAsync(
            PatientBuilder.Build(includeName: false, includeGender: false));
        Patient? noNameUnknownGender = await FhirClient.CreateAsync(
            PatientBuilder.Build(includeName: false));
        Patient? namedNoGender = await FhirClient.CreateAsync(
            PatientBuilder.Build(familyName: "Smith", includeGender: false));
        Assert.NotNull(noNameNoGender);
        Assert.NotNull(noNameUnknownGender);
        Assert.NotNull(namedNoGender);

        Bundle? bundle = await FhirClient.SearchAsync<Patient>(
            new[] { "name:missing=true", "gender:not=unknown" });

        Assert.NotNull(bundle);
        Assert.Equal([noNameNoGender.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_ByCodeWithEmptySystemPrefix_MatchesOnlyCodingWithNoSystem()
    {
        // Spec section 5.1 guard. FHIR R4: '[parameter]=|[code]' matches where the Coding has NO
        // system property. No existing test covered this form, so without it the claim that the
        // ':not' rework removes no capability would be unguarded.
        var noSystem = ObservationBuilder.Build();
        noSystem.Code = new CodeableConcept
        {
            Coding = [new Coding { Code = BodyWeightCode }] // no System
        };

        Observation? withoutSystem = await FhirClient.CreateAsync(noSystem);
        Observation? withSystem = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: BodyWeightCode));
        Assert.NotNull(withoutSystem);
        Assert.NotNull(withSystem);

        Bundle? bundle = await FhirClient.SearchAsync<Observation>(
            new[] { $"code=|{BodyWeightCode}" });

        Assert.NotNull(bundle);
        Assert.Equal([withoutSystem.Id], bundle.Entry.Select(e => e.Resource!.Id).Order());
    }

    [Fact]
    public async Task Search_CodeMissing_TreatsNullSystemRowAsPresent()
    {
        // Review Focus 5: presence is presence. A coding with no system still produces an
        // IndexToken row, so ':missing=false' must match it and ':missing=true' must not.
        // Asserted via both directions on a single resource, because Observation.code is 1..1
        // and there is therefore no codeless Observation to contrast against.
        var observation = ObservationBuilder.Build();
        observation.Code = new CodeableConcept
        {
            Coding = [new Coding { Code = BodyWeightCode }] // no System
        };

        Observation? noSystem = await FhirClient.CreateAsync(observation);
        Assert.NotNull(noSystem);

        Bundle? present = await FhirClient.SearchAsync<Observation>(new[] { "code:missing=false" });
        Assert.NotNull(present);
        Assert.Equal([noSystem.Id], present.Entry.Select(e => e.Resource!.Id).Order());

        Bundle? absent = await FhirClient.SearchAsync<Observation>(new[] { "code:missing=true" });
        Assert.NotNull(absent);
        Assert.Empty(absent.Entry);
    }

    private async Task CreateObservationAsync(string? loincCode = null)
    {
        Observation? observation = await FhirClient.CreateAsync(
            ObservationBuilder.Build(loincCode: loincCode));
        Assert.NotNull(observation);
    }
}
