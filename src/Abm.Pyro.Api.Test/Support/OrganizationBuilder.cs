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
            // Always set: an Organization with no name and nothing else serialises to an empty
            // object, which the FHIR parser rejects ("Empty FHIR elements are invalid").
            Active = true,
            Name = includeName ? name ?? "TestOrganization" : null
        };
    }
}
