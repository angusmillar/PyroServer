using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.FhirSupport;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace Abm.Pyro.Application.FhirPatch;

public sealed class FhirPathPatchService(
    IFhirSerializationSupport fhirSerializationSupport,
    IFhirDeSerializationSupport fhirDeSerializationSupport) : IFhirPathPatchService
{
    private readonly record struct PatchOperation(
        string Type,
        string Path,
        string? Name,
        DataType? Value,
        int? Index,
        int? Source,
        int? Destination);

    public Resource Apply(Resource target, Parameters patchParameters)
    {
        string json = fhirSerializationSupport.ToJson(target, SummaryType.False, pretty: false);
        JsonObject root = JsonNode.Parse(json)!.AsObject();

        foreach (Parameters.ParameterComponent parameter in patchParameters.Parameter.Where(p => p.Name == "operation"))
        {
            PatchOperation op = ExtractOperation(parameter);
            ApplyOperation(root, op);
        }

        string patchedJson = root.ToJsonString();
        return fhirDeSerializationSupport.ToResource(patchedJson)
               ?? throw new ApplicationException("Failed to parse patched resource from JSON.");
    }

    private PatchOperation ExtractOperation(Parameters.ParameterComponent parameter)
    {
        string? type     = GetPartValue<Code>(parameter, "type")?.Value;
        string? path     = GetPartValue<FhirString>(parameter, "path")?.Value;
        string? name     = GetPartValue<FhirString>(parameter, "name")?.Value;
        DataType? value  = GetPartDataType(parameter, "value");
        int? index       = GetPartValue<Integer>(parameter, "index")?.Value;
        int? source      = GetPartValue<Integer>(parameter, "source")?.Value;
        int? destination = GetPartValue<Integer>(parameter, "destination")?.Value;

        if (type is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "A patch operation parameter is missing the required 'type' part.");
        if (path is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "A patch operation parameter is missing the required 'path' part.");

        return new PatchOperation(type, path, name, value, index, source, destination);
    }

    private static T? GetPartValue<T>(Parameters.ParameterComponent parameter, string partName) where T : DataType
        => parameter.Part.FirstOrDefault(p => p.Name == partName)?.Value as T;

    private static DataType? GetPartDataType(Parameters.ParameterComponent parameter, string partName)
        => parameter.Part.FirstOrDefault(p => p.Name == partName)?.Value;

    private void ApplyOperation(JsonObject root, PatchOperation op)
    {
        switch (op.Type.ToLowerInvariant())
        {
            case "add":     ApplyAdd(root, op);     break;
            case "insert":  ApplyInsert(root, op);  break;
            case "delete":  ApplyDelete(root, op);  break;
            case "replace": ApplyReplace(root, op); break;
            case "move":    ApplyMove(root, op);    break;
            default:
                throw new FhirErrorException(HttpStatusCode.BadRequest,
                    $"Unknown FHIRPath patch operation type '{op.Type}'. " +
                    "Valid values are: add, insert, delete, replace, move.");
        }
    }

    private void ApplyAdd(JsonObject root, PatchOperation op)
    {
        if (op.Name is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'add' operation requires a 'name' part.");
        if (op.Value is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'add' operation requires a 'value' part.");

        JsonObject parent = NavigateToObject(root, op.Path, op.Type);
        JsonNode valueNode = DataTypeToJsonNode(op.Value);

        if (!parent.TryGetPropertyValue(op.Name, out JsonNode? existing) || existing is null)
        {
            parent[op.Name] = valueNode.DeepClone();
        }
        else if (existing is JsonArray array)
        {
            array.Add(valueNode.DeepClone());
        }
        else
        {
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                $"Patch 'add': element '{op.Name}' at path '{op.Path}' already has a value. " +
                "Use 'replace' to modify an existing single-value element, or 'insert' to append to a list.");
        }
    }

    private void ApplyInsert(JsonObject root, PatchOperation op)
    {
        if (!op.Index.HasValue)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'insert' operation requires an 'index' part.");
        if (op.Value is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'insert' operation requires a 'value' part.");

        (JsonObject parent, string propName, _) = NavigateToParentAndProp(root, op.Path, op.Type);

        if (!parent.TryGetPropertyValue(propName, out JsonNode? node) || node is not JsonArray array)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                $"Patch 'insert': path '{op.Path}' did not resolve to a JSON array.");

        if (op.Index.Value < 0 || op.Index.Value > array.Count)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                $"Patch 'insert': index {op.Index.Value} is out of range for list of length {array.Count}.");

        JsonNode valueNode = DataTypeToJsonNode(op.Value);
        array.Insert(op.Index.Value, valueNode.DeepClone());
    }

    private static void ApplyDelete(JsonObject root, PatchOperation op)
    {
        (JsonObject parent, string propName, int? index) = NavigateToParentAndProp(root, op.Path, op.Type);

        if (index.HasValue)
        {
            if (!parent.TryGetPropertyValue(propName, out JsonNode? node) || node is not JsonArray array)
                throw new FhirErrorException(HttpStatusCode.BadRequest,
                    $"Patch 'delete': path '{op.Path}' did not resolve to a JSON array.");

            if (index.Value < 0 || index.Value >= array.Count)
                throw new FhirErrorException(HttpStatusCode.BadRequest,
                    $"Patch 'delete': index {index.Value} is out of range for list of length {array.Count}.");

            array.RemoveAt(index.Value);
        }
        else
        {
            if (!parent.ContainsKey(propName))
                throw new FhirErrorException(HttpStatusCode.BadRequest,
                    $"Patch 'delete': element at path '{op.Path}' does not exist.");

            parent.Remove(propName);
            parent.Remove("_" + propName); // Remove primitive extension sibling if present
        }
    }

    private void ApplyReplace(JsonObject root, PatchOperation op)
    {
        if (op.Value is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'replace' operation requires a 'value' part.");

        (JsonObject parent, string propName, int? index) = NavigateToParentAndProp(root, op.Path, op.Type);
        JsonNode valueNode = DataTypeToJsonNode(op.Value);

        if (index.HasValue)
        {
            if (!parent.TryGetPropertyValue(propName, out JsonNode? node) || node is not JsonArray array)
                throw new FhirErrorException(HttpStatusCode.BadRequest,
                    $"Patch 'replace': path '{op.Path}' did not resolve to a JSON array.");

            if (index.Value < 0 || index.Value >= array.Count)
                throw new FhirErrorException(HttpStatusCode.BadRequest,
                    $"Patch 'replace': index {index.Value} is out of range for list of length {array.Count}.");

            array[index.Value] = valueNode.DeepClone();
        }
        else
        {
            if (!parent.ContainsKey(propName))
                throw new FhirErrorException(HttpStatusCode.BadRequest,
                    $"Patch 'replace': element at path '{op.Path}' does not exist. Use 'add' to create a new element.");

            parent[propName] = valueNode.DeepClone();
        }
    }

    private static void ApplyMove(JsonObject root, PatchOperation op)
    {
        if (!op.Source.HasValue || !op.Destination.HasValue)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'move' operation requires both 'source' and 'destination' parts.");

        (JsonObject parent, string propName, _) = NavigateToParentAndProp(root, op.Path, op.Type);

        if (!parent.TryGetPropertyValue(propName, out JsonNode? node) || node is not JsonArray array)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                $"Patch 'move': path '{op.Path}' did not resolve to a JSON array.");

        int src  = op.Source.Value;
        int dest = op.Destination.Value;

        if (src < 0 || src >= array.Count || dest < 0 || dest >= array.Count)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                $"Patch 'move': source {src} or destination {dest} is out of range for list of length {array.Count}.");

        JsonNode item = array[src]!.DeepClone();
        array.RemoveAt(src);
        array.Insert(dest, item);
    }

    private static JsonObject NavigateToObject(JsonObject root, string fhirPath, string opType)
    {
        List<(string Name, int? Index)> segments = ParsePath(fhirPath);

        if (segments.Count == 0)
            return root;

        JsonNode? current = root;
        foreach ((string name, int? index) in segments)
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(name, out JsonNode? child) || child is null)
                throw PathNotFound(fhirPath, opType);

            current = child;

            if (index.HasValue)
            {
                if (current is not JsonArray arr || index.Value < 0 || index.Value >= arr.Count)
                    throw PathNotFound(fhirPath, opType);
                current = arr[index.Value];
            }
        }

        return current as JsonObject
               ?? throw new FhirErrorException(HttpStatusCode.BadRequest,
                   $"Patch '{opType}': path '{fhirPath}' resolved to a non-object node.");
    }

    private static (JsonObject Parent, string PropName, int? Index) NavigateToParentAndProp(
        JsonObject root, string fhirPath, string opType)
    {
        List<(string Name, int? Index)> segments = ParsePath(fhirPath);

        if (segments.Count == 0)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                $"Patch '{opType}': path '{fhirPath}' must address a child element, not the resource root.");

        (string lastName, int? lastIndex) = segments[^1];
        var parentSegments = segments.Take(segments.Count - 1);

        JsonNode? parent = root;
        foreach ((string name, int? index) in parentSegments)
        {
            if (parent is not JsonObject obj || !obj.TryGetPropertyValue(name, out JsonNode? child) || child is null)
                throw PathNotFound(fhirPath, opType);

            parent = child;

            if (index.HasValue)
            {
                if (parent is not JsonArray arr || index.Value < 0 || index.Value >= arr.Count)
                    throw PathNotFound(fhirPath, opType);
                parent = arr[index.Value];
            }
        }

        return parent as JsonObject is { } parentObj
            ? (parentObj, lastName, lastIndex)
            : throw PathNotFound(fhirPath, opType);
    }

    private static List<(string Name, int? Index)> ParsePath(string fhirPath)
    {
        var segments = new List<(string Name, int? Index)>();
        bool skipFirst = true;

        foreach (string part in fhirPath.Split('.'))
        {
            if (skipFirst)
            {
                skipFirst = false;
                continue; // Skip the resource type prefix (e.g., "Patient")
            }

            string name  = part;
            int? index   = null;

            int bracketOpen = part.IndexOf('[');
            if (bracketOpen >= 0)
            {
                int bracketClose = part.IndexOf(']', bracketOpen);
                if (bracketClose > bracketOpen &&
                    int.TryParse(part[(bracketOpen + 1)..bracketClose], out int parsedIndex))
                {
                    name  = part[..bracketOpen];
                    index = parsedIndex;
                }
            }

            segments.Add((name, index));
        }

        return segments;
    }

    private JsonNode DataTypeToJsonNode(DataType value)
    {
        // Wrap the DataType in a Parameters resource so the Firely serializer produces
        // correct FHIR JSON (handles primitive types, complex types, and their extensions).
        var wrapper = new Parameters();
        wrapper.Add("v", value);

        string wrappedJson = fhirSerializationSupport.ToJson(wrapper, SummaryType.False, pretty: false);

        using JsonDocument doc = JsonDocument.Parse(wrappedJson);
        JsonElement paramElement = doc.RootElement.GetProperty("parameter")[0];

        foreach (JsonProperty prop in paramElement.EnumerateObject())
        {
            if (prop.Name.StartsWith("value", StringComparison.OrdinalIgnoreCase))
                return JsonNode.Parse(prop.Value.GetRawText())!;
        }

        throw new FhirErrorException(HttpStatusCode.BadRequest,
            $"Could not serialize patch value of type '{value.GetType().Name}' to FHIR JSON.");
    }

    private static FhirErrorException PathNotFound(string path, string opType)
        => new(HttpStatusCode.BadRequest,
            $"Patch '{opType}': path '{path}' did not match any element in the resource.");
}
