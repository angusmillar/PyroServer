namespace Abm.Pyro.Api.Test.Support;

public static class ObservationBuilder
{
    public static Hl7.Fhir.Model.Observation Build(
        string? subjectPatientId = null,
        string? loincCode = null,
        decimal? valueQuantityAmount = null,
        string? valueQuantityUnit = null,
        string? snomedCode = null)
    {
        var observation = new Hl7.Fhir.Model.Observation
        {
            Status = Hl7.Fhir.Model.ObservationStatus.Final
        };

        // Observation.code has minimum cardinality 1 in FHIR R4, so there is deliberately no way
        // to omit it here: a codeless Observation cannot be created and the server rejects it
        // with a 400. Tests needing an absent token value use an optional element instead, such
        // as Patient.gender.
        var coding = new List<Hl7.Fhir.Model.Coding>
        {
            new()
            {
                System = CodeSystemUriSupport.Loinc,
                Code = loincCode ?? "29463-7",
                Display = "Body weight"
            }
        };

        if (snomedCode is not null)
        {
            coding.Add(new Hl7.Fhir.Model.Coding
            {
                System = CodeSystemUriSupport.Snomed,
                Code = snomedCode
            });
        }

        observation.Code = new Hl7.Fhir.Model.CodeableConcept { Coding = coding };

        if (subjectPatientId is not null)
        {
            observation.Subject = new Hl7.Fhir.Model.ResourceReference($"Patient/{subjectPatientId}");
        }

        if (valueQuantityAmount.HasValue)
        {
            observation.Value = new Hl7.Fhir.Model.Quantity
            {
                Value = valueQuantityAmount.Value,
                Unit = valueQuantityUnit ?? "kg",
                System = CodeSystemUriSupport.Ucum,
                Code = valueQuantityUnit ?? "kg"
            };
        }

        return observation;
    }
}
