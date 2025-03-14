using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.FhirSupport;
using Firely.Fhir.Packages;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using Microsoft.Extensions.Options;
using Firely.Fhir.Validation;

namespace Abm.Pyro.Application.FhirValidateService;

public class FhirValidateEngine : IFhirValidateEngine
{
    private readonly Validator _validator;
    private readonly IOperationOutcomeSupport OperationOutcomeSupport;

    public FhirValidateEngine(
        IAsyncResourceResolver asyncResourceResolver,
        IOptions<FhirValidationSettings> fhirValidationSettingsOptions,
        IOperationOutcomeSupport operationOutcomeSupport)
    {
        OperationOutcomeSupport = operationOutcomeSupport;
        Uri? packageServerUrl = fhirValidationSettingsOptions.Value.ProfilePackageServiceUrl;
        var fhirRelease = FhirRelease.R4;

        var packageResolver = FhirPackageSource.CreateCorePackageSource(ModelInfo.ModelInspector, fhirRelease,
            packageServerUrl!.OriginalString);

        // Finally, we combine both sources, so we will find profiles both from the core zip and from the directory.
        // By mentioning the directory source first, anything in the user directory will override what is in the core zip.
        MultiResolver multiResolver = new MultiResolver(packageResolver, asyncResourceResolver);

        var resourceResolver = new CachedResolver(multiResolver);
        
        Uri? terminologyServiceUrl = fhirValidationSettingsOptions.Value.TerminologyServiceUrl;
        ITerminologyService terminologyService = new ExternalTerminologyService(new FhirClient(terminologyServiceUrl));
        
        _validator = new Validator(resourceResolver, terminologyService);
    }

    public OperationOutcome Validate(Resource resourceToValidate)
    {
        List<Uri> profileUrlList = FhirValidateSupport.GetProfileListFromResource(resourceToValidate);
        return Validate(resource: resourceToValidate, profileUriList: profileUrlList);
    }

    public OperationOutcome Validate(
        Resource resource,
        List<Uri> profileUriList)
    {
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

            return OperationOutcomeSupport.MergeOperationOutcomeList(validationResultOperationOutcomeList);
            
        }
        catch (SchemaResolutionFailedException sfe)
        {
            return OperationOutcomeSupport.GetError([$"Failed to load the profile: {sfe.SchemaUri}"]);
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