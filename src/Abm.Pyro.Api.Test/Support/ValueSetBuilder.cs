namespace Abm.Pyro.Api.Test.Support;

public static class ValueSetBuilder
{
    public static Hl7.Fhir.Model.ValueSet Build(
        string? url = null,
        string? name = null,
        bool includeUrl = true)
    {
        return new Hl7.Fhir.Model.ValueSet
        {
            Url = includeUrl ? url ?? "http://example.org/fhir/ValueSet/test-valueset" : null,
            Name = name ?? "TestValueSet",
            Status = Hl7.Fhir.Model.PublicationStatus.Draft
        };
    }
}
