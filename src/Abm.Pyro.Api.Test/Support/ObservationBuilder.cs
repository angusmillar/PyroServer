namespace Abm.Pyro.Api.Test.Support;

public static class ObservationBuilder
{
    public static Hl7.Fhir.Model.Observation Build(
        string? subjectPatientId = null,
        string? loincCode = null,
        decimal? valueQuantityAmount = null,
        string? valueQuantityUnit = null)
    {
        var observation = new Hl7.Fhir.Model.Observation
        {
            Status = Hl7.Fhir.Model.ObservationStatus.Final,
            Code = new Hl7.Fhir.Model.CodeableConcept
            {
                Coding =
                [
                    new Hl7.Fhir.Model.Coding
                    {
                        System = CodeSystemUriSupport.Loinc,
                        Code = loincCode ?? "29463-7",
                        Display = "Body weight"
                    }
                ]
            }
        };

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
