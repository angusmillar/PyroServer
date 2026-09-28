namespace Abm.Pyro.Api.Test.Support;

public static class OrganizationBuilder
{
    public static Hl7.Fhir.Model.Organization Build(
        string? id = null,
        string? name = null,
        bool includeName = true)
    {
        return new Hl7.Fhir.Model.Organization
        {
            Id = id,
            Name = includeName ? name ?? "TestOrganization" : null
        };
    }
}
