using System.Net;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.FhirValidate;
using Abm.Pyro.Domain.Notification;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirValidateService;

public class FhirValidateOperationService(
    IFhirValidateEngine fhirValidateEngine,
    IRepositoryEventCollector repositoryEventCollector,
    [FromKeyedServices(FhirOperationLevel.Type)] IValidatorBase<FhirTypeLevelOperationRequest> typeLevelRequestValidator,
    [FromKeyedServices(FhirOperationLevel.Instance)] IValidatorBase<FhirInstanceLevelOperationRequest> instanceLevelRequestValidator) 
    : IFhirValidateOperationService
{
    public const string OperationName = "validate";

    public FhirResourceResponse Handle(FhirInstanceLevelOperationRequest request)
    {
        ValidatorResult validatorResult = instanceLevelRequestValidator.Validate(request);
        if (!validatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(validatorResult);
        }
        
        Resource? resourceToValidate = null;
        FhirValidateMode? mode = null;
        Uri? profile = null;
        
        if (EndpointResourceTypeEqualsBodyResourceType(request.ResourceName, request.Resource.TypeName))
        {
            FhirValidateRequest fhirValidateRequest = FhirValidateSupport.GetRequestFromQuery(request.QueryString);
            resourceToValidate = request.Resource;
            mode = fhirValidateRequest.Mode;
            profile = GetProfileAsUri(fhirValidateRequest.Profile);
        }

        if (request.Resource is Parameters parameters)
        {
            FhirValidateRequest fhirValidateRequest = FhirValidateSupport.GetRequestFromParameterResource(parameters);
            resourceToValidate = fhirValidateRequest.Resource;
            mode = fhirValidateRequest.Mode;
            profile = GetProfileAsUri(fhirValidateRequest.Profile);
        }
        
        if (mode.Equals(FhirValidateMode.Update))
        {
            //Nothing to do here?
            //See: https://hl7.org/fhir/R4/valueset-resource-validation-mode.html
            //The server checks the content, and then checks that it would accept it as an update against the nominated
            //specific resource (e.g. that there are no changes to immutable fields the server does not allow to change
            //and checking version integrity if appropriate).
        }
        
        if (mode.Equals(FhirValidateMode.Delete))
        {
            //Nothing to do here?
            //See: https://hl7.org/fhir/R4/valueset-resource-validation-mode.html
            //The server ignores the content and checks that the nominated resource is allowed to be deleted
            //(e.g. checking referential integrity rules).
        }
        
        ArgumentNullException.ThrowIfNull(resourceToValidate);
        
        OperationOutcome operationOutcome = fhirValidateEngine.Validate(
            resource: resourceToValidate, 
            profileUriList: GetListOfProfiles(profile, resourceToValidate));

        return SuccessfulResultResponse(operationOutcome: operationOutcome);
        
    }

    public FhirResourceResponse Handle(FhirTypeLevelOperationRequest request)
    {
        ValidatorResult validatorResult = typeLevelRequestValidator.Validate(request);
        if (!validatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(validatorResult);
        }

        Resource? resourceToValidate = null;
        FhirValidateMode? mode = null;
        Uri? profile = null;
        
        if (EndpointResourceTypeEqualsBodyResourceType(request.ResourceName, request.Resource.TypeName))
        {
            FhirValidateRequest fhirValidateRequest = FhirValidateSupport.GetRequestFromQuery(request.QueryString);
            resourceToValidate = request.Resource;
            mode = fhirValidateRequest.Mode;
            profile = GetProfileAsUri(fhirValidateRequest.Profile);
        }

        if (request.Resource is Parameters parameters)
        {
            FhirValidateRequest fhirValidateRequest = FhirValidateSupport.GetRequestFromParameterResource(parameters);
            resourceToValidate = fhirValidateRequest.Resource;
            mode = fhirValidateRequest.Mode;
            profile = GetProfileAsUri(fhirValidateRequest.Profile);
        }
        
        if (mode.Equals(FhirValidateMode.Create))
        {
            //Nothing to do here?
            //The server checks the content, and then checks that the content would be acceptable as a Create
            //(e.g. that the content would not violate any uniqueness constraints).
        }
        
        ArgumentNullException.ThrowIfNull(resourceToValidate);
        
        OperationOutcome operationOutcome = fhirValidateEngine.Validate(
            resource: resourceToValidate, 
            profileUriList: GetListOfProfiles(profile, resourceToValidate));

        return SuccessfulResultResponse(operationOutcome: operationOutcome);
        
    }

    private List<Uri> GetListOfProfiles(
        Uri? profile,
        Resource resourceToValidate)
    {
        if (profile is not null)
        {
            return new List<Uri>() { profile }; 
        }

        return FhirValidateSupport.GetProfileListFromResource(resourceToValidate);
    }

    
    
    
    private static Uri? GetProfileAsUri(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return null;
        }

        if (Uri.TryCreate(profile, UriKind.Absolute, out Uri? profileUri)) 
        {
            return profileUri; 
        }
        
        throw new InvalidCastException(nameof(profile));
        
    }

    private static bool EndpointResourceTypeEqualsBodyResourceType(
        string requestEndpointResourceName, 
        string requestBodyResourceName)
    {
        return requestEndpointResourceName.Equals(requestBodyResourceName);
    }

    private FhirResourceResponse InvalidValidatorResultResponse(ValidatorResult validatorResult)
    {
        repositoryEventCollector.Clear();
        return new FhirResourceResponse(
            Resource: validatorResult.GetOperationOutcome(), 
            HttpStatusCode: validatorResult.GetHttpStatusCode(),
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
    
    private FhirResourceResponse SuccessfulResultResponse(OperationOutcome operationOutcome)
    {
        repositoryEventCollector.Clear();
        return new FhirResourceResponse(
            Resource: operationOutcome, 
            HttpStatusCode: HttpStatusCode.OK,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: repositoryEventCollector);
    }
}