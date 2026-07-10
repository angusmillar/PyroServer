namespace Abm.Pyro.Api.Test.Support;

public static class PatientBuilder
{
    public static Hl7.Fhir.Model.Patient Build(
        string? id = null,
        string? familyName = null,
        string? givenName = null,
        string? deceasedDateTime = null,
        string? managingOrganizationId = null)
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

        if (deceasedDateTime is not null)
        {
            patient.Deceased = new Hl7.Fhir.Model.FhirDateTime(deceasedDateTime);
        }

        if (managingOrganizationId is not null)
        {
            patient.ManagingOrganization = new Hl7.Fhir.Model.ResourceReference($"Organization/{managingOrganizationId}");
        }

        return patient;
    }
}
