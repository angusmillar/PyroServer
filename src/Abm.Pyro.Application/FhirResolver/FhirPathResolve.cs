using System.Net;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Abm.Pyro.Domain.FhirSupport;
using FhirUri = Abm.Pyro.Domain.FhirSupport.FhirUri;

namespace Abm.Pyro.Application.FhirResolver;

public class FhirPathResolve(
    IFhirUriFactory fhirUriFactory) : IFhirPathResolve
{
    public ITypedElement Resolver(string url)
    {
        var defaultModelFactory = new DefaultModelFactory();
        if (fhirUriFactory.TryParse(url, out FhirUri? fhirUri, out string errorMessage))
        {
            if (fhirUri.IsOperation || fhirUri.IsUrn)
            {
                return GetOperationOutcomeResourceReference(defaultModelFactory);
            }
            
            Type? resourceType = ModelInfo.GetTypeForFhirType(fhirUri.ResourceName);
      
            if (resourceType is null)
            {
                throw new ApplicationException($"Unable to find a FHIR domain resource of type '{fhirUri.ResourceName}'.");
            }
            
            if (defaultModelFactory.Create(resourceType) is DomainResource domainResource)
            {
                domainResource.Id = fhirUri.ResourceId;

                return domainResource.ToTypedElement().ToScopedNode();
            }

            throw new ApplicationException($"Unable to create a FHIR domain resource of type '{resourceType.Name}'.");
        }

        return GetOperationOutcomeResourceReference(defaultModelFactory);
        
        
        //If this below is a problem in the future, you could return the above commented out code which would ignore
        //the invalid reference.
        // throw new FhirErrorException(
        //     httpStatusCode: HttpStatusCode.BadRequest, 
        //     message: $"A FHIR resource reference is invalid. Reference: {url}. {errorMessage}");
    }

    private static ITypedElement GetOperationOutcomeResourceReference(DefaultModelFactory defaultModelFactory)
    {
        if (defaultModelFactory.Create(ModelInfo.GetTypeForFhirType(FhirResourceTypeId.OperationOutcome.GetCode())) is DomainResource operationOutcomeResource)
        {
            operationOutcomeResource.Id = "temp";
            return operationOutcomeResource.ToTypedElement().ToScopedNode();
        }

        throw new ApplicationException(
            $"Unable to find a FHIR domain resource of type '{FhirResourceTypeId.OperationOutcome.GetCode()}'.");
    }


    // public ITypedElement? ResolverOLD(string url)
    // {
    //   if (fhirUriFactory.TryParse(url, out FhirUri? fhirUri, out string _))
    //   {
    //     var defaultModelFactory = new Hl7.Fhir.Serialization.DefaultModelFactory();
    //     Type? type = ModelInfo.GetTypeForFhirType(fhirUri.ResourceName);
    //     if (type is null)
    //     {
    //       return null;
    //       //throw new ApplicationException($"ResourceName of '{fhirUri.ResourceName}' can not be converted to a FHIR Type.");
    //     }
    //     if (defaultModelFactory.Create(type) is DomainResource domainResource)
    //     {
    //       domainResource.Id = fhirUri.ResourceId;
    //       return domainResource.ToTypedElement().ToScopedNode();
    //     }
    //     throw new ApplicationException($"Unable to create a domain resource of type '{type.Name}'.");
    //   }
    //   return null;
    // }
}