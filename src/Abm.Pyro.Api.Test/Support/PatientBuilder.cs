namespace Abm.Pyro.Api.Test.Support;

public static class PatientBuilder
{
    public static Hl7.Fhir.Model.Patient Build(string? id = null, string? familyName = null, string? givenName = null)
    {
        var patient = new Hl7.Fhir.Model.Patient
        {
            Id = id,
            Name =
            [
                new Hl7.Fhir.Model.HumanName
                {
                    Family = familyName ?? "TestFamily",
                    Given = [givenName ?? "TestGiven"]
                }
            ],
            BirthDate = "1990-01-15",
            Gender = Hl7.Fhir.Model.AdministrativeGender.Unknown
        };
        return patient;
    }
}
