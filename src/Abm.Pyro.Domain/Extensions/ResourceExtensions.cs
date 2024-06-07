using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification;

namespace Abm.Pyro.Domain.Extensions;

//https://github.com/brianpos/fhir-net-web-api/blob/8e73b9154ddd086588e1d51de9984c989163b9a3/src/Hl7.Fhir.WebApi.Support/ResourceExtensions.cs#L116
public static class ResourceExtensions
{
    private const string ResourceReferenceToken = "ResourceReference";
    private const string ResourceToken = "Resource";
    private const string BackboneElementToken = "BackboneElement";
    private const string UriToken = "FhirUri";
    private const string UrlToken = "FhirUrl";
    private const string UuidToken = "Uuid";
    private const string OidToken = "Oid";
    public static List<ResourceReference> AllReferenceList(this Base? resourceModelBase)
    {
        List<ResourceReference> results = new List<ResourceReference>();
        if (resourceModelBase is null)
        {
            return results;
        }
            
        if (resourceModelBase is IExtendable extendable)
        {
            foreach (var extension in extendable.Extension)
            {
                AddIfTypeIsResourceReference(extension.Value, results);
            }
        }

        if (resourceModelBase is IModifierExtendable modExtendable)
        {
            foreach (var extension in modExtendable.ModifierExtension)
            {
                AddIfTypeIsResourceReference(extension.Value, results);
            }
        }

        ClassMapping.TryGetMappingForType(resourceModelBase.GetType(), FhirRelease.R4, out ClassMapping? mapping);
        if (mapping is null)
        {
            throw new NullReferenceException(nameof(mapping));
        }

        foreach (var propertyMap in mapping.PropertyMappings)
        {
            string test = propertyMap.Name;
            string test2 = propertyMap.ImplementingType.Name;
            
        }
        
        IEnumerable<PropertyMapping> propertyMappingList = mapping.PropertyMappings.Where(x =>
            x.ImplementingType.Name.Equals(ResourceReferenceToken, StringComparison.Ordinal) ||
            x.ImplementingType.Name.Equals(ResourceToken, StringComparison.Ordinal) ||
            x.ImplementingType.Name.Equals(UriToken, StringComparison.Ordinal) ||
            x.ImplementingType.Name.Equals(UrlToken, StringComparison.Ordinal) ||
            x.Choice == ChoiceType.DatatypeChoice ||
            (x.ImplementingType.BaseType is not null && x.ImplementingType.BaseType.Name.Equals(BackboneElementToken, StringComparison.Ordinal)));

        foreach (PropertyMapping propertyMapping in propertyMappingList)
        {
            if (IsBackboneElement(propertyMapping))
            {
                if (propertyMapping.IsCollection)
                {
                    //Method calls recursively back to this method     
                    GetResourceReferencesFromBackboneElementCollection(propertyMapping);
                }
                else
                {
                    //Method calls recursively back to this method
                    GetResourceReferencesFromBackboneElement(propertyMapping);
                }
            }
            else if (IsResource(propertyMapping))
            {
                if (propertyMapping.IsCollection)
                {
                    //Method calls recursively back to this method
                    GetResourceReferencesFromResourceCollection(propertyMapping);
                }
                else
                {
                    //Method calls recursively back to this method
                    GetResourceReferencesFromResource(propertyMapping);
                }
            }
            else
            {
                if (propertyMapping.ImplementingType.Name.Equals(UriToken, StringComparison.Ordinal))
                {
                    
                }
                if (propertyMapping.ImplementingType.Name.Equals(UrlToken, StringComparison.Ordinal))
                {
                    
                }
                if (propertyMapping.ImplementingType.Name.Equals(OidToken, StringComparison.Ordinal))
                {
                                    
                }
                if (propertyMapping.ImplementingType.Name.Equals(UuidToken, StringComparison.Ordinal))
                {
                                    
                }
                if (propertyMapping.IsCollection)
                {
                    string test = propertyMapping.ImplementingType.Name;
                    object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
                    if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
                    {
                        foreach (object item in propertyValueCollection)
                        {
                            AddIfTypeIsResourceReference(item, results);
                        }
                    }
                }
                else
                {
                    object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
                    AddIfTypeIsResourceReference(propertyValueObject, results);
                }
            }
        }

        return results;

        void GetResourceReferencesFromBackboneElementCollection(PropertyMapping propertyMapping)
        {
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is null)
            {
                throw new NullReferenceException(nameof(propertyValueObject));
            }

            if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
            {
                foreach (object? item in propertyValueCollection)
                {
                    if (item is BackboneElement backboneElement)
                    {
                        results.AddRange(backboneElement.AllReferenceList()); //recursive call    
                    }
                }
            }
        }

        void GetResourceReferencesFromBackboneElement(PropertyMapping propertyMapping)
        {
            object? backboneElementObject = propertyMapping.GetValue(resourceModelBase);
            if (backboneElementObject is not null && backboneElementObject is BackboneElement backboneElement)
            {
                results.AddRange(backboneElement.AllReferenceList()); //recursive call  
            }
        }
        
        void GetResourceReferencesFromResourceCollection(PropertyMapping propertyMapping)
        {
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is null)
            {
                throw new NullReferenceException(nameof(propertyValueObject));
            }

            if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
            {
                foreach (object? item in propertyValueCollection)
                {
                    if (item is Resource resource)
                    {
                        results.AddRange(resource.AllReferenceList()); //recursive call    
                    }
                }
            }
        }

        void GetResourceReferencesFromResource(PropertyMapping propertyMapping)
        {
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is Resource resource)
            {
                results.AddRange(resource.AllReferenceList()); //recursive call   
            }
        }
    }

    public static List<FhirUrl> AllUrlList(this Base? resourceModelBase)
    {
        List<FhirUrl> results = new List<FhirUrl>();
        if (resourceModelBase is null)
        {
            return results;
        }
            
        if (resourceModelBase is IExtendable extendable)
        {
            foreach (var extension in extendable.Extension)
            {
                AddIfTypeIsFhirUrl(extension.Value, results);
            }
        }

        if (resourceModelBase is IModifierExtendable modExtendable)
        {
            foreach (var extension in modExtendable.ModifierExtension)
            {
                AddIfTypeIsFhirUrl(extension.Value, results);
            }
        }

        ClassMapping.TryGetMappingForType(resourceModelBase.GetType(), FhirRelease.R4, out ClassMapping? mapping);
        if (mapping is null)
        {
            throw new NullReferenceException(nameof(mapping));
        }

        foreach (var propertyMap in mapping.PropertyMappings)
        {
            string test = propertyMap.Name;
            string test2 = propertyMap.ImplementingType.Name;
            
        }
        
        IEnumerable<PropertyMapping> propertyMappingList = mapping.PropertyMappings.Where(x =>
            x.ImplementingType.Name.Equals(UrlToken, StringComparison.Ordinal) ||
            x.ImplementingType.Name.Equals(ResourceToken, StringComparison.Ordinal) ||
            x.Choice == ChoiceType.DatatypeChoice ||
            (x.ImplementingType.BaseType is not null && x.ImplementingType.BaseType.Name.Equals(BackboneElementToken, StringComparison.Ordinal)));

        foreach (PropertyMapping propertyMapping in propertyMappingList)
        {
            if (IsBackboneElement(propertyMapping))
            {
                if (propertyMapping.IsCollection)
                {
                    //Method calls recursively back to this method     
                    GetFhirUrlFromBackboneElementCollection(propertyMapping);
                }
                else
                {
                    //Method calls recursively back to this method
                    GetFhirUrlFromBackboneElement(propertyMapping);
                }
            }
            else if (IsResource(propertyMapping))
            {
                if (propertyMapping.IsCollection)
                {
                    //Method calls recursively back to this method
                    GetFhirUrlFromResourceCollection(propertyMapping);
                }
                else
                {
                    //Method calls recursively back to this method
                    GetFhirUrlFromResource(propertyMapping);
                }
            }
            else
            {
               
                if (propertyMapping.ImplementingType.Name.Equals(UrlToken, StringComparison.Ordinal))
                {
                    
                }
                
                if (propertyMapping.IsCollection)
                {
                    string test = propertyMapping.ImplementingType.Name;
                    object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
                    if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
                    {
                        foreach (object item in propertyValueCollection)
                        {
                            AddIfTypeIsFhirUrl(item, results);
                        }
                    }
                }
                else
                {
                    object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
                    AddIfTypeIsFhirUrl(propertyValueObject, results);
                }
            }
        }

        return results;
        
        void GetFhirUrlFromBackboneElementCollection(PropertyMapping propertyMapping)
        {
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is null)
            {
                throw new NullReferenceException(nameof(propertyValueObject));
            }

            if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
            {
                foreach (object? item in propertyValueCollection)
                {
                    if (item is BackboneElement backboneElement)
                    {
                        results.AddRange(backboneElement.AllUrlList()); //recursive call    
                    }
                }
            }
        }

        void GetFhirUrlFromBackboneElement(PropertyMapping propertyMapping)
        {
            object? backboneElementObject = propertyMapping.GetValue(resourceModelBase);
            if (backboneElementObject is not null && backboneElementObject is BackboneElement backboneElement)
            {
                results.AddRange(backboneElement.AllUrlList()); //recursive call  
            }
        }

        void GetFhirUrlFromResourceCollection(PropertyMapping propertyMapping)
        {
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is null)
            {
                throw new NullReferenceException(nameof(propertyValueObject));
            }

            if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
            {
                foreach (object? item in propertyValueCollection)
                {
                    if (item is Resource resource)
                    {
                        results.AddRange(resource.AllUrlList()); //recursive call    
                    }
                }
            }
        }

        void GetFhirUrlFromResource(PropertyMapping propertyMapping)
        {
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is Resource resource)
            {
                results.AddRange(resource.AllUrlList()); //recursive call   
            }
        }
    }

    public static List<FhirUri> AllUriList(this Base? resourceModelBase)
    {
        List<FhirUri> results = new List<FhirUri>();
        if (resourceModelBase is null)
        {
            return results;
        }
            
        if (resourceModelBase is IExtendable extendable)
        {
            foreach (var extension in extendable.Extension)
            {
                AddIfTypeIsFhirUri(extension.Value, results);
            }
        }

        if (resourceModelBase is IModifierExtendable modExtendable)
        {
            foreach (var extension in modExtendable.ModifierExtension)
            {
                AddIfTypeIsFhirUri(extension.Value, results);
            }
        }

        ClassMapping.TryGetMappingForType(resourceModelBase.GetType(), FhirRelease.R4, out ClassMapping? mapping);
        if (mapping is null)
        {
            throw new NullReferenceException(nameof(mapping));
        }

        foreach (var propertyMap in mapping.PropertyMappings)
        {
            string test = propertyMap.Name;
            string test2 = propertyMap.ImplementingType.Name;
            
        }
        
        IEnumerable<PropertyMapping> propertyMappingList = mapping.PropertyMappings.Where(x =>
            x.ImplementingType.Name.Equals(UriToken, StringComparison.Ordinal) ||
            x.ImplementingType.Name.Equals(ResourceToken, StringComparison.Ordinal) ||
            x.Choice == ChoiceType.DatatypeChoice ||
            (x.ImplementingType.BaseType is not null && x.ImplementingType.BaseType.Name.Equals(BackboneElementToken, StringComparison.Ordinal)));

        foreach (PropertyMapping propertyMapping in propertyMappingList)
        {
            if (IsBackboneElement(propertyMapping))
            {
                if (propertyMapping.IsCollection)
                {
                    //Method calls recursively back to this method     
                    GetFhirUriFromBackboneElementCollection(propertyMapping);
                }
                else
                {
                    //Method calls recursively back to this method
                    GetFhirUriFromBackboneElement(propertyMapping);
                }
            }
            else if (IsResource(propertyMapping))
            {
                if (propertyMapping.IsCollection)
                {
                    //Method calls recursively back to this method
                    GetFhirUriFromResourceCollection(propertyMapping);
                }
                else
                {
                    //Method calls recursively back to this method
                    GetFhirUriFromResource(propertyMapping);
                }
            }
            else
            {
               
                if (propertyMapping.ImplementingType.Name.Equals(UrlToken, StringComparison.Ordinal))
                {
                    
                }
                
                if (propertyMapping.IsCollection)
                {
                    string test = propertyMapping.ImplementingType.Name;
                    object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
                    if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
                    {
                        foreach (object item in propertyValueCollection)
                        {
                            AddIfTypeIsFhirUri(item, results);
                        }
                    }
                }
                else
                {
                    object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
                    AddIfTypeIsFhirUri(propertyValueObject, results);
                }
            }
        }

        return results;
        
        void GetFhirUriFromBackboneElementCollection(PropertyMapping propertyMapping)
        {
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is null)
            {
                throw new NullReferenceException(nameof(propertyValueObject));
            }

            if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
            {
                foreach (object? item in propertyValueCollection)
                {
                    if (item is BackboneElement backboneElement)
                    {
                        results.AddRange(backboneElement.AllUriList()); //recursive call    
                    }
                }
            }
        }

        void GetFhirUriFromBackboneElement(PropertyMapping propertyMapping)
        {
            object? backboneElementObject = propertyMapping.GetValue(resourceModelBase);
            if (backboneElementObject is not null && backboneElementObject is BackboneElement backboneElement)
            {
                results.AddRange(backboneElement.AllUriList()); //recursive call  
            }
        }

        void GetFhirUriFromResourceCollection(PropertyMapping propertyMapping)
        {
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is null)
            {
                throw new NullReferenceException(nameof(propertyValueObject));
            }

            if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
            {
                foreach (object? item in propertyValueCollection)
                {
                    if (item is Resource resource)
                    {
                        results.AddRange(resource.AllUriList()); //recursive call    
                    }
                }
            }
        }

        void GetFhirUriFromResource(PropertyMapping propertyMapping)
        {
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is Resource resource)
            {
                results.AddRange(resource.AllUriList()); //recursive call   
            }
        }
    }

    public static List<Uuid> AllUuidList(this Base? resourceModelBase)
    {
        List<Uuid> results = new List<Uuid>();
        if (resourceModelBase is null)
        {
            return results;
        }
            
        if (resourceModelBase is IExtendable extendable)
        {
            foreach (var extension in extendable.Extension)
            {
                AddIfTypeIsUuid(extension.Value, results);
            }
        }

        if (resourceModelBase is IModifierExtendable modExtendable)
        {
            foreach (var extension in modExtendable.ModifierExtension)
            {
                AddIfTypeIsUuid(extension.Value, results);
            }
        }

        ClassMapping.TryGetMappingForType(resourceModelBase.GetType(), FhirRelease.R4, out ClassMapping? mapping);
        if (mapping is null)
        {
            throw new NullReferenceException(nameof(mapping));
        }

        foreach (var propertyMap in mapping.PropertyMappings)
        {
            string test = propertyMap.Name;
            string test2 = propertyMap.ImplementingType.Name;
            
        }
        
        IEnumerable<PropertyMapping> propertyMappingList = mapping.PropertyMappings.Where(x =>
            x.ImplementingType.Name.Equals(UuidToken, StringComparison.Ordinal) ||
            x.ImplementingType.Name.Equals(ResourceToken, StringComparison.Ordinal) ||
            x.Choice == ChoiceType.DatatypeChoice ||
            (x.ImplementingType.BaseType is not null && x.ImplementingType.BaseType.Name.Equals(BackboneElementToken, StringComparison.Ordinal)));

        foreach (PropertyMapping propertyMapping in propertyMappingList)
        {
            if (IsBackboneElement(propertyMapping))
            {
                if (propertyMapping.IsCollection)
                {
                    //Method calls recursively back to this method     
                    GetUuidFromBackboneElementCollection(propertyMapping);
                }
                else
                {
                    //Method calls recursively back to this method
                    GetUuidFromBackboneElement(propertyMapping);
                }
            }
            else if (IsResource(propertyMapping))
            {
                if (propertyMapping.IsCollection)
                {
                    //Method calls recursively back to this method
                    GetUuidFromResourceCollection(propertyMapping);
                }
                else
                {
                    //Method calls recursively back to this method
                    GetUuidFromResource(propertyMapping);
                }
            }
            else
            {
               
                if (propertyMapping.ImplementingType.Name.Equals(UrlToken, StringComparison.Ordinal))
                {
                    
                }
                
                if (propertyMapping.IsCollection)
                {
                    string test = propertyMapping.ImplementingType.Name;
                    object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
                    if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
                    {
                        foreach (object item in propertyValueCollection)
                        {
                            AddIfTypeIsUuid(item, results);
                        }
                    }
                }
                else
                {
                    object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
                    AddIfTypeIsUuid(propertyValueObject, results);
                }
            }
        }

        return results;
        
        void GetUuidFromBackboneElementCollection(PropertyMapping propertyMapping)
        {
            ArgumentNullException.ThrowIfNull(resourceModelBase);
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is null)
            {
                throw new NullReferenceException(nameof(propertyValueObject));
            }

            if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
            {
                foreach (object? item in propertyValueCollection)
                {
                    if (item is BackboneElement backboneElement)
                    {
                        results.AddRange(backboneElement.AllUuidList()); //recursive call    
                    }
                }
            }
        }

        void GetUuidFromBackboneElement(PropertyMapping propertyMapping)
        {
            ArgumentNullException.ThrowIfNull(resourceModelBase);
            object? backboneElementObject = propertyMapping.GetValue(resourceModelBase);
            if (backboneElementObject is not null && backboneElementObject is BackboneElement backboneElement)
            {
                results.AddRange(backboneElement.AllUuidList()); //recursive call  
            }
        }

        void GetUuidFromResourceCollection(PropertyMapping propertyMapping)
        {
            ArgumentNullException.ThrowIfNull(resourceModelBase);
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is null)
            {
                throw new NullReferenceException(nameof(propertyValueObject));
            }

            if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
            {
                foreach (object? item in propertyValueCollection)
                {
                    if (item is Resource resource)
                    {
                        results.AddRange(resource.AllUuidList()); //recursive call    
                    }
                }
            }
        }

        void GetUuidFromResource(PropertyMapping propertyMapping)
        {
            ArgumentNullException.ThrowIfNull(resourceModelBase);
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is Resource resource)
            {
                results.AddRange(resource.AllUuidList()); //recursive call   
            }
        }
    }
    
    public static List<Oid> AllOidList(this Base? resourceModelBase)
    {
        List<Oid> results = new List<Oid>();
        if (resourceModelBase is null)
        {
            return results;
        }
            
        if (resourceModelBase is IExtendable extendable)
        {
            foreach (var extension in extendable.Extension)
            {
                AddIfTypeIsOid(extension.Value, results);
            }
        }

        if (resourceModelBase is IModifierExtendable modExtendable)
        {
            foreach (var extension in modExtendable.ModifierExtension)
            {
                AddIfTypeIsOid(extension.Value, results);
            }
        }

        ClassMapping.TryGetMappingForType(resourceModelBase.GetType(), FhirRelease.R4, out ClassMapping? mapping);
        if (mapping is null)
        {
            throw new NullReferenceException(nameof(mapping));
        }

        foreach (var propertyMap in mapping.PropertyMappings)
        {
            string test = propertyMap.Name;
            string test2 = propertyMap.ImplementingType.Name;
            
        }
        
        IEnumerable<PropertyMapping> propertyMappingList = mapping.PropertyMappings.Where(x =>
            x.ImplementingType.Name.Equals(OidToken, StringComparison.Ordinal) ||
            x.ImplementingType.Name.Equals(ResourceToken, StringComparison.Ordinal) ||
            x.Choice == ChoiceType.DatatypeChoice ||
            (x.ImplementingType.BaseType is not null && x.ImplementingType.BaseType.Name.Equals(BackboneElementToken, StringComparison.Ordinal)));

        foreach (PropertyMapping propertyMapping in propertyMappingList)
        {
            if (IsBackboneElement(propertyMapping))
            {
                if (propertyMapping.IsCollection)
                {
                    //Method calls recursively back to this method     
                    GetOidFromBackboneElementCollection(propertyMapping);
                }
                else
                {
                    //Method calls recursively back to this method
                    GetOidFromBackboneElement(propertyMapping);
                }
            }
            else if (IsResource(propertyMapping))
            {
                if (propertyMapping.IsCollection)
                {
                    //Method calls recursively back to this method
                    GetOidFromResourceCollection(propertyMapping);
                }
                else
                {
                    //Method calls recursively back to this method
                    GetOidFromResource(propertyMapping);
                }
            }
            else
            {
               
                if (propertyMapping.ImplementingType.Name.Equals(UrlToken, StringComparison.Ordinal))
                {
                    
                }
                
                if (propertyMapping.IsCollection)
                {
                    string test = propertyMapping.ImplementingType.Name;
                    object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
                    if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
                    {
                        foreach (object item in propertyValueCollection)
                        {
                            AddIfTypeIsOid(item, results);
                        }
                    }
                }
                else
                {
                    object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
                    AddIfTypeIsOid(propertyValueObject, results);
                }
            }
        }

        return results;
        
        void GetOidFromBackboneElementCollection(PropertyMapping propertyMapping)
        {
            ArgumentNullException.ThrowIfNull(resourceModelBase);
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is null)
            {
                throw new NullReferenceException(nameof(propertyValueObject));
            }

            if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
            {
                foreach (object? item in propertyValueCollection)
                {
                    if (item is BackboneElement backboneElement)
                    {
                        results.AddRange(backboneElement.AllOidList()); //recursive call    
                    }
                }
            }
        }

        void GetOidFromBackboneElement(PropertyMapping propertyMapping)
        {
            ArgumentNullException.ThrowIfNull(resourceModelBase);
            object? backboneElementObject = propertyMapping.GetValue(resourceModelBase);
            if (backboneElementObject is not null && backboneElementObject is BackboneElement backboneElement)
            {
                results.AddRange(backboneElement.AllOidList()); //recursive call  
            }
        }

        void GetOidFromResourceCollection(PropertyMapping propertyMapping)
        {
            ArgumentNullException.ThrowIfNull(resourceModelBase);
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is null)
            {
                throw new NullReferenceException(nameof(propertyValueObject));
            }

            if (propertyValueObject is System.Collections.IEnumerable propertyValueCollection)
            {
                foreach (object? item in propertyValueCollection)
                {
                    if (item is Resource resource)
                    {
                        results.AddRange(resource.AllOidList()); //recursive call    
                    }
                }
            }
        }

        void GetOidFromResource(PropertyMapping propertyMapping)
        {
            ArgumentNullException.ThrowIfNull(resourceModelBase);
            object? propertyValueObject = propertyMapping.GetValue(resourceModelBase);
            if (propertyValueObject is Resource resource)
            {
                results.AddRange(resource.AllOidList()); //recursive call   
            }
        }
    }
    
    private static bool IsBackboneElement(PropertyMapping propertyMapping)
    {
        return propertyMapping.ImplementingType.BaseType is not null && propertyMapping.ImplementingType.BaseType.Name == BackboneElementToken;
    }
    private static bool IsResource(PropertyMapping propertyMapping)
    {
        return propertyMapping.ImplementingType.Name.Equals(ResourceToken, StringComparison.Ordinal);
    }
    private static void AddIfTypeIsResourceReference(object? obj, List<ResourceReference> results)
    {
        if (obj is ResourceReference resRef)
        {
            results.Add(resRef);
        }
    }
    private static void AddIfTypeIsFhirUrl(object? obj, List<FhirUrl> results)
    {
        if (obj is FhirUrl fhirUrl)
        {
            results.Add(fhirUrl);
        }
    }
    private static void AddIfTypeIsFhirUri(object? obj, List<FhirUri> results)
    {
        if (obj is FhirUri fhirUri)
        {
            results.Add(fhirUri);
        }
    }
    private static void AddIfTypeIsUuid(object? obj, List<Uuid> results)
    {
        if (obj is Uuid uuid)
        {
            results.Add(uuid);
        }
    }
    private static void AddIfTypeIsOid(object? obj, List<Oid> results)
    {
        if (obj is Oid oid)
        {
            results.Add(oid);
        }
    }
}