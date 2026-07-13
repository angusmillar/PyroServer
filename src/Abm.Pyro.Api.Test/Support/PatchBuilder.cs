using Hl7.Fhir.Model;

namespace Abm.Pyro.Api.Test.Support;

public static class PatchBuilder
{
    public static Parameters.ParameterComponent MakeOp(
        string    type,
        string    path,
        string?   name        = null,
        DataType? value       = null,
        int?      index       = null,
        int?      source      = null,
        int?      destination = null)
    {
        var parameterComponent = new Parameters.ParameterComponent
        {
            Name = "operation",
            Part =
            [
                new() { Name = "type", Value = new Code(type) },
                new() { Name = "path", Value = new FhirString(path) }
            ]
        };
        if (name        is not null) parameterComponent.Part.Add(new() { Name = "name",        Value = new FhirString(name) });
        if (value       is not null) parameterComponent.Part.Add(new() { Name = "value",       Value = value });
        if (index       is not null) parameterComponent.Part.Add(new() { Name = "index",       Value = new Integer(index) });
        if (source      is not null) parameterComponent.Part.Add(new() { Name = "source",      Value = new Integer(source) });
        if (destination is not null) parameterComponent.Part.Add(new() { Name = "destination", Value = new Integer(destination) });
        
        return parameterComponent;
    }

    public static Parameters GetParameters(
        List<Parameters.ParameterComponent> patchOperationList)
    {
        var p = new Parameters();
        p.Parameter.AddRange(patchOperationList);
        return p;
    }
}