using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.FhirSupport;
using Firely.Fhir.Packages;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using Firely.Fhir.Validation;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirValidateService;

public class FhirValidateEngine(
    IAsyncResourceResolver asyncResourceResolver,
    IServiceSettingsCache serviceSettingsCache,
    IOperationOutcomeSupport operationOutcomeSupport)
    : IFhirValidateEngine
{
    private Validator? _validator;

    private async Task InitialiseValidator()
    {
        
        // FhirPackageSource packageResolver = new FhirPackageSource(
        //     provider: ModelInfo.ModelInspector, 
        //     new string[]
        //     {
        //         "C:\\Temp\\FHIR Packages\\hl7.fhir.r4.core#4.0.1",
        //         "C:\\Temp\\FHIR Packages\\hl7.fhir.r4.expansions#4.0.1",
        //         "C:\\Temp\\FHIR Packages\\hl7.fhir.uv.extensions.r4#1.0.0"
        //     });
        
        var fhirValidationSettings = await serviceSettingsCache.GetFhirValidationSettings();
        Uri? packageServerUrl = fhirValidationSettings.ProfilePackageServiceUrl;
        var fhirRelease = FhirRelease.R4;
        
        //Downloads the Core FHIR profiles package from the internet 
        IAsyncResourceResolver? packageResolver = FhirPackageSource.CreateCorePackageSource(
            provider: ModelInfo.ModelInspector, 
            version: fhirRelease,
            packageServer: packageServerUrl!.OriginalString);

        // Finally, we combine both sources, so we will find profiles both from the core zip and from the directory.
        // By mentioning the directory source first, anything in the user directory will override what is in the core zip.
        MultiResolver multiResolver = new MultiResolver(packageResolver, asyncResourceResolver);

        //This resourceResolver really should be a singleton??? 
        var resourceResolver = new CachedResolver(multiResolver);
        
        Uri? terminologyServiceUrl = fhirValidationSettings.TerminologyServiceUrl;
        ITerminologyService terminologyService = new ExternalTerminologyService(new FhirClient(terminologyServiceUrl));
        
        _validator = new Validator(resourceResolver, terminologyService);
    }
    
    public async Task<OperationOutcome> Validate(Resource resourceToValidate)
    {
        if (_validator is null)
        {
            await InitialiseValidator();
        }
        
        List<Uri> profileUrlList = FhirValidateSupport.GetProfileListFromResource(resourceToValidate);
        return await Validate(resource: resourceToValidate, profileUriList: profileUrlList);
    }

    public async Task<OperationOutcome> Validate(
        Resource resource,
        List<Uri> profileUriList)
    {
        if (_validator is null)
        {
            await InitialiseValidator();
        }
        
        ArgumentNullException.ThrowIfNull(_validator);
        
        try
        {
            var validationResultOperationOutcomeList = new List<OperationOutcome>();
            foreach (var profile in profileUriList)
            {
                validationResultOperationOutcomeList.Add(_validator.Validate(resource, profile.OriginalString));
            }
            
            if (IfNoValidationIssues(validationResultOperationOutcomeList))
            {
                return GetSuccessOperationOutcome();
            }

            return operationOutcomeSupport.MergeOperationOutcomeList(validationResultOperationOutcomeList);
            
        }
        catch (SchemaResolutionFailedException sfe)
        {
            return operationOutcomeSupport.GetError([$"Failed to load the profile: {sfe.SchemaUri}"]);
        }
    }

    private static bool IfNoValidationIssues(
        List<OperationOutcome> validationResultOperationOutcomeList)
    {
        return validationResultOperationOutcomeList.All(x => x.Success) &&
               validationResultOperationOutcomeList.All(x => x.Issue.Count == 0);
    }

    private static OperationOutcome GetSuccessOperationOutcome()
    {
        return new OperationOutcome()
        {
            Id = "allok",
            Issue = new List<OperationOutcome.IssueComponent>()
            {
                new()
                {
                    Severity = OperationOutcome.IssueSeverity.Information,
                    Code = OperationOutcome.IssueType.Informational,
                    Details = new CodeableConcept() { Text = "All OK" }
                }
            }
        };
    }
}