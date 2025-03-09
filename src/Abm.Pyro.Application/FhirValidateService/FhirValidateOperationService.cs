using System.Net;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.FhirOperation;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.Notification;
using Abm.Pyro.Domain.Validation;
using Firely.Fhir.Packages;
using Firely.Fhir.Validation;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using Hl7.Fhir.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirValidateService;

public class FhirValidateOperationService(
    IAsyncResourceResolver asyncResourceResolver,
    IRepositoryEventCollector repositoryEventCollector,
    [FromKeyedServices(FhirOperationLevel.System)] IValidatorBase<FhirSystemLevelOperationRequest> requestValidator) : IFhirValidateOperationService
{
    public const string OperationName = "validate";
    
    public async Task<FhirResourceResponse> Handle(FhirSystemLevelOperationRequest request)
    {
        
        ValidatorResult validatorResult = requestValidator.Validate(request);
        if (!validatorResult.IsValid)
        {
            return InvalidValidatorResultResponse(validatorResult);
        }
        
        if (request.Resource is not Parameters requestParameters)
        {
            throw new FhirFatalException(
                httpStatusCode: HttpStatusCode.ServiceUnavailable, 
                message: $"The FHIR ${OperationName} operation must be provided a resource type of: " +
                         $"{ResourceType.Parameters.GetLiteral()}, encountered type of: {request.Resource.TypeName}. ");
        }

        ValidateRequestParameters validateRequestParameters = GetValidateRequestParameters(requestParameters);
        
        
        var packageServerUrl = "https://packages.simplifier.net";
        var fhirRelease = FhirRelease.R4;
        
        var packageResolver = FhirPackageSource.CreateCorePackageSource(ModelInfo.ModelInspector, fhirRelease, packageServerUrl);
        
        
        
        
        // Finally, we combine both sources, so we will find profiles both from the core zip as well as from the directory.
        // By mentioning the directory source first, anything in the user directory will override what is in the core zip.
        MultiResolver multiResolver = new MultiResolver(packageResolver, asyncResourceResolver);
        
        var resourceResolver = new CachedResolver(multiResolver);
        string ontoServer = "https://tx.dev.hl7.org.au/fhir";
        ITerminologyService terminologyService = new ExternalTerminologyService(new FhirClient(ontoServer));
        //LocalTerminologyService terminologyService = new LocalTerminologyService(resourceResolver);
        try
        {
            var validator = new Validator(resourceResolver, terminologyService);
            Resource testResource = validateRequestParameters.Resource!;
            
            string profile = validateRequestParameters.Profile!;
            var result = validator.Validate(testResource, profile);

            if (result.Success)
            {
                result.Id = "allok";
                result.Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Information,
                        Code = OperationOutcome.IssueType.Informational,
                        Details = new CodeableConcept() { Text = "All OK" }
                    }
                };
            }
            return new FhirResourceResponse(
                Resource: result,
                HttpStatusCode: HttpStatusCode.OK,
                Headers: new Dictionary<string, StringValues>(),
                ResourceOutcomeInfo: null,
                RepositoryEventCollector: repositoryEventCollector);

            
        }
        catch (SchemaResolutionFailedException e)
        {
            Console.WriteLine(e);
            throw;
        }
        
        
        //var profile = Canonical.ForCoreType("Organization").ToString();
        
        
        //Terminology Server for Sparked 
        //https://tx.dev.hl7.org.au/fhir
        await Task.Delay(1000);
        throw new NotImplementedException();

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

    private record ValidateRequestParameters(
        string? Mode,
        string? Profile,
        Resource? Resource);

    private ValidateRequestParameters GetValidateRequestParameters(
        Parameters parameters)
    {
        Parameters.ParameterComponent? modeParameter =
            parameters.Parameter.FirstOrDefault(x => x.Name.Equals("mode", StringComparison.OrdinalIgnoreCase));
        string? mode = null;
        if (modeParameter?.Value is Code code)
        {
            mode = code.Value.Trim();
        }

        Parameters.ParameterComponent? profileParameter =
            parameters.Parameter.FirstOrDefault(x => x.Name.Equals("profile", StringComparison.OrdinalIgnoreCase));
        string? profile = null;
        if (profileParameter?.Value is FhirUri fhirUri)
        {
            profile = fhirUri.Value.Trim();
        }

        Parameters.ParameterComponent? resourceParameter =
            parameters.Parameter.FirstOrDefault(x => x.Name.Equals("resource", StringComparison.OrdinalIgnoreCase));
        Resource? resource = resourceParameter?.Resource;

        return new ValidateRequestParameters(Mode: mode, Profile: profile, Resource: resource);
    }
    
    public string Handle(string test)
    {
        throw new NotImplementedException();
    }

}