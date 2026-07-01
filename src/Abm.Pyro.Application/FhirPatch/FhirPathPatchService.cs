using System.Net;
using Abm.Pyro.Domain.Exceptions;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.FhirPatch;

public sealed class FhirPathPatchService : IFhirPathPatchService
{
    private readonly record struct PatchOperation(
        string Type,
        string Path,
        string? Name,
        DataType? Value,
        int? Index,
        int? Source,
        int? Destination);

    private readonly record struct PathSegment(string Name, int? Index);

    public Resource Apply(Resource target, Parameters patchParameters)
    {
        IEnumerable<Parameters.ParameterComponent> operations =
            patchParameters.Parameter.Where(p => p.Name == "operation").ToList();

        if (!operations.Any())
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "The patch Parameters resource contains no 'operation' entries. At least one operation is required.");

        ElementNode mutableTree = ElementNode.FromElement(new ScopedNode(target.ToTypedElement()));

        foreach (Parameters.ParameterComponent parameter in operations)
            ApplyOperation(mutableTree, ExtractOperation(parameter));

        return mutableTree.ToPoco<Resource>(ModelInfo.ModelInspector)
               ?? throw new ApplicationException("Failed to convert patched element tree back to FHIR resource.");
    }

    private static void ApplyOperation(ElementNode root, PatchOperation op)
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

    // path → parent object; name → new property to add; value → what to add
    private static void ApplyAdd(ElementNode root, PatchOperation op)
    {
        if (op.Name is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'add' operation requires a 'name' part.");
        if (op.Value is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'add' operation requires a 'value' part.");

        ElementNode parent = NavigatePath(root, op.Path, "add");
        ElementNode newChild = ElementNode.FromElement(op.Value.ToTypedElement());
        parent.Add(ModelInfo.ModelInspector, newChild, op.Name);
    }

    // path → the repeating property (e.g. "Patient.name"); insert value at index
    private static void ApplyInsert(ElementNode root, PatchOperation op)
    {
        if (!op.Index.HasValue)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'insert' operation requires an 'index' part.");
        if (op.Value is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'insert' operation requires a 'value' part.");

        (ElementNode parent, string propName) = NavigateToParentProp(root, op.Path, "insert");
        List<ElementNode> siblings = parent.Children(propName).Cast<ElementNode>().ToList();
        int idx = op.Index.Value;

        if (idx < 0 || idx > siblings.Count)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                $"Patch 'insert': index {idx} is out of range for list of length {siblings.Count}.");

        ElementNode newChild = ElementNode.FromElement(op.Value.ToTypedElement());

        // Remove and re-add siblings in the new order with the new element at idx
        foreach (ElementNode sib in siblings) parent.Remove(sib);
        for (int i = 0; i < idx; i++) parent.Add(ModelInfo.ModelInspector, siblings[i]);
        parent.Add(ModelInfo.ModelInspector, newChild, propName);
        for (int i = idx; i < siblings.Count; i++) parent.Add(ModelInfo.ModelInspector, siblings[i]);
    }

    // path → the specific element to remove (may include index, e.g. Patient.name[0])
    private static void ApplyDelete(ElementNode root, PatchOperation op)
    {
        ElementNode target = NavigatePath(root, op.Path, "delete");

        ElementNode parent = target.Parent
                             ?? throw new FhirErrorException(HttpStatusCode.BadRequest,
                                 $"Patch 'delete': path '{op.Path}' resolved to the resource root, which cannot be deleted.");

        if (!parent.Remove(target))
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                $"Patch 'delete': failed to remove element at path '{op.Path}'.");
    }

    // path → the specific element to replace; value → replacement
    private static void ApplyReplace(ElementNode root, PatchOperation op)
    {
        if (op.Value is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'replace' operation requires a 'value' part.");

        ElementNode target = NavigatePath(root, op.Path, "replace");

        if (op.Value is PrimitiveType primitive)
        {
            // Primitive: update the leaf value in-place; node stays in the tree
            target.Value = primitive.ObjectValue;
            return;
        }

        // Complex: replace the node in its parent, preserving position
        ElementNode parent = target.Parent
                             ?? throw new FhirErrorException(HttpStatusCode.BadRequest,
                                 $"Patch 'replace': path '{op.Path}' resolved to the resource root, which cannot be replaced.");

        ElementNode replacement = ElementNode.FromElement(op.Value.ToTypedElement());
        replacement.Name = target.Name;
        parent.Replace(ModelInfo.ModelInspector, target, replacement);
    }

    // path → the repeating property (e.g. "Patient.name"); reorder source → destination
    private static void ApplyMove(ElementNode root, PatchOperation op)
    {
        if (!op.Source.HasValue || !op.Destination.HasValue)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "Patch 'move' operation requires both 'source' and 'destination' parts.");

        (ElementNode parent, string propName) = NavigateToParentProp(root, op.Path, "move");
        List<ElementNode> siblings = parent.Children(propName).Cast<ElementNode>().ToList();
        int src  = op.Source.Value;
        int dest = op.Destination.Value;

        if (src < 0 || src >= siblings.Count || dest < 0 || dest >= siblings.Count)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                $"Patch 'move': source {src} or destination {dest} is out of range for list of length {siblings.Count}.");

        ElementNode item = siblings[src];
        siblings.RemoveAt(src);
        siblings.Insert(dest, item);

        foreach (ElementNode sib in parent.Children(propName).Cast<ElementNode>().ToList()) parent.Remove(sib);
        foreach (ElementNode sib in siblings) parent.Add(ModelInfo.ModelInspector, sib);
    }

    // Navigate directly through ElementNode.Children() so mutations affect the real tree.
    // FHIRPath paths in the patch spec are simple navigation paths, e.g.:
    //   Patient                     → root itself
    //   Patient.name                → single name (no index → must be unambiguous)
    //   Patient.name[0]             → first name element
    //   Patient.name[0].given[1]    → second given of first name
    private static ElementNode NavigatePath(ElementNode root, string fhirPath, string opType)
    {
        List<PathSegment> segments = ParseSegments(fhirPath);
        return Traverse(root, segments, fhirPath, opType);
    }

    // Returns the parent node and the final property name from the path.
    // "Patient.name[0].given" → (Patient.name[0] node, "given")
    // Used for operations that act on the array (insert/move) or parent (add).
    private static (ElementNode Parent, string PropName) NavigateToParentProp(
        ElementNode root, string fhirPath, string opType)
    {
        List<PathSegment> segments = ParseSegments(fhirPath);

        if (segments.Count == 0)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                $"Patch '{opType}': path '{fhirPath}' must reference a child element, not the resource root.");

        PathSegment lastSeg = segments[^1];
        if (lastSeg.Index.HasValue)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                $"Patch '{opType}': path '{fhirPath}' must not end with an index — specify the array name only (e.g. 'Patient.name').");

        ElementNode parent = Traverse(root, segments.Take(segments.Count - 1), fhirPath, opType);
        return (parent, lastSeg.Name);
    }

    private static ElementNode Traverse(
        ElementNode start,
        IEnumerable<PathSegment> segments,
        string fhirPath,
        string opType)
    {
        ElementNode current = start;

        foreach ((string name, int? index) in segments)
        {
            List<ElementNode> children = current.Children(name).Cast<ElementNode>().ToList();

            if (children.Count == 0)
                throw new FhirErrorException(HttpStatusCode.BadRequest,
                    $"Patch '{opType}': path '{fhirPath}' — element '{name}' not found.");

            if (index.HasValue)
            {
                if (index.Value < 0 || index.Value >= children.Count)
                    throw new FhirErrorException(HttpStatusCode.BadRequest,
                        $"Patch '{opType}': path '{fhirPath}' — index [{index.Value}] out of range (list has {children.Count} item(s)).");
                current = children[index.Value];
            }
            else if (children.Count == 1)
            {
                current = children[0];
            }
            else
            {
                throw new FhirErrorException(HttpStatusCode.BadRequest,
                    $"Patch '{opType}': path '{fhirPath}' — element '{name}' has {children.Count} occurrences; use an index (e.g. '{name}[0]') to select one.");
            }
        }

        return current;
    }

    // "Patient.name[0].given[1]" → [("name", 0), ("given", 1)]  (resource type prefix skipped)
    private static List<PathSegment> ParseSegments(string fhirPath)
    {
        var segments = new List<PathSegment>();

        foreach (string part in fhirPath.Split('.').Skip(1)) // skip resource type prefix
        {
            string name  = part;
            int?   index = null;

            int bracketOpen = part.IndexOf('[');
            if (bracketOpen >= 0)
            {
                int bracketClose = part.IndexOf(']', bracketOpen);
                if (bracketClose > bracketOpen &&
                    int.TryParse(part[(bracketOpen + 1)..bracketClose], out int parsedIdx))
                {
                    name  = part[..bracketOpen];
                    index = parsedIdx;
                }
            }

            segments.Add(new PathSegment(name, index));
        }

        return segments;
    }

    private static PatchOperation ExtractOperation(Parameters.ParameterComponent parameter)
    {
        string?   type        = GetPartValue<Code>(parameter, "type")?.Value;
        string?   path        = GetPartValue<FhirString>(parameter, "path")?.Value;
        string?   name        = GetPartValue<FhirString>(parameter, "name")?.Value;
        DataType? value       = GetPartDataType(parameter, "value");
        int?      index       = GetPartValue<Integer>(parameter, "index")?.Value;
        int?      source      = GetPartValue<Integer>(parameter, "source")?.Value;
        int?      destination = GetPartValue<Integer>(parameter, "destination")?.Value;

        if (type is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "A patch operation is missing the required 'type' part.");
        if (path is null)
            throw new FhirErrorException(HttpStatusCode.BadRequest,
                "A patch operation is missing the required 'path' part.");

        return new PatchOperation(type, path, name, value, index, source, destination);
    }

    private static T? GetPartValue<T>(Parameters.ParameterComponent parameter, string partName) where T : DataType
        => parameter.Part.FirstOrDefault(p => p.Name == partName)?.Value as T;

    private static DataType? GetPartDataType(Parameters.ParameterComponent parameter, string partName)
        => parameter.Part.FirstOrDefault(p => p.Name == partName)?.Value;
}
