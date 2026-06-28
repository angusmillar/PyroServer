namespace Abm.Pyro.Api.Test.Support;

public static class ValueSetBuilder
{
    public static Hl7.Fhir.Model.ValueSet Build(string? url = null, string? name = null)
    {
        return new Hl7.Fhir.Model.ValueSet
        {
            Url = url ?? "http://example.org/fhir/ValueSet/test-valueset",
            Name = name ?? "TestValueSet",
            Status = Hl7.Fhir.Model.PublicationStatus.Draft
        };
    }
}
