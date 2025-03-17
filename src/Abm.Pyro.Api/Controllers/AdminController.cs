using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.ServiceSettingRequest;
using Abm.Pyro.Domain.ServiceSettings;
using Abm.Pyro.Domain.Support;
using Microsoft.AspNetCore.Mvc;
using Hl7.Fhir.Model;
using MediatR;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Controllers;

[Route("admin/{tenant}")]
[ApiController]
public class AdminController(
    IFhirParameterSupport fhirParameterSupport,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : ControllerBase
{
    // [HttpGet("SearchParameter/{resourceId}/{history}/{historyId}")]
    // public async Task<ActionResult> Search(
    //     string tenant,
    //     string resourceId,
    //     string history,
    //     string historyId,
    //     CancellationToken cancellationToken)
    // {
    //     await Task.Delay(2000, cancellationToken);
    //     logger.LogInformation("Search parameter called : {Tenant}, {ResourceId}, {History}, {HistoryId}", tenant,
    //         resourceId, history, historyId);
    //
    //     bool isConnected = await _context.Database.CanConnectAsync(cancellationToken);
    //
    //     return Ok();
    // }

    [HttpGet("Filter")]
    public async Task<ActionResult> Search(
        string tenant,
        CancellationToken cancellationToken)
    {
        await Task.Delay(2000, cancellationToken);
        return Ok();
    }

    // GET: admin/{tenant}/FhirValidation/_history
    // [HttpGet("FhirValidation/_history")]
    // public async Task<ActionResult<IEnumerable<ServiceConfiguration>>> GetServiceConfigurations()
    // {
    //     return await _context.ServiceConfiguration.ToListAsync();
    // }

    //GET: admin/{tenant}/FhirValidation
    [HttpGet("settings/FhirValidation")]
    public async Task<ActionResult<Parameters>> GetServiceConfiguration()
    {
        FhirValidationSettingsGetResponse fhirValidationSettingsGetResponse =  await mediator.Send(new FhirValidationSettingsGetRequest());
        
        return GetParametersResource(fhirValidationSettingsGetResponse.FhirValidationSettings);
        
    }


    // PUT: admin/{tenant}/settings/FhirValidation

    [HttpPut("settings/FhirValidation")]
    public async Task<ActionResult<Parameters>> UpdateFhirValidationServiceSetting(Parameters parameters)
    {
        Uri? profilePackageServiceUrl = fhirParameterSupport.GetParameterFhirUrlValue("ProfilePackageServiceUrl", parameters.Parameter);
        Uri? terminologyServiceUrl = fhirParameterSupport.GetParameterFhirUrlValue("TerminologyServiceUrl", parameters.Parameter);
        bool? validateOnCreate = fhirParameterSupport.GetParameterFhirBoolValue("ValidateOnCreate", parameters.Parameter);
        bool? validateOnUpdate = fhirParameterSupport.GetParameterFhirBoolValue("ValidateOnUpdate", parameters.Parameter);

        var fhirValidationSettingsUpdateRequest = new FhirValidationSettingsUpdateRequest(
            FhirValidationSettings: new FhirValidationSettings(
                versionId: parameters.Meta.VersionId,
                profilePackageServiceUrl: profilePackageServiceUrl,
                terminologyServiceUrl: terminologyServiceUrl,
                validateOnCreate: validateOnCreate ?? false, //defaults to false if null
                validateOnUpdate: validateOnUpdate ?? false, //defaults to false if null
                lastUpdated: dateTimeProvider.Now.DateTime));
        
        //return BadRequest();
        FhirValidationSettingsUpdateResponse fhirValidationSettingsUpdateResponse =  await mediator.Send(fhirValidationSettingsUpdateRequest);

        return GetParametersResource(fhirValidationSettingsUpdateResponse.FhirValidationSettings);

    }

    private static Parameters GetParametersResource(FhirValidationSettings fhirValidationSettings)
    {

        var parameterList = new List<Parameters.ParameterComponent>();

        if (!string.IsNullOrWhiteSpace(fhirValidationSettings.ProfilePackageServiceUrl?.OriginalString))
        {
            parameterList.Add(new Parameters.ParameterComponent()
            {
                Name = "ProfilePackageServiceUrl",
                Value = new FhirUrl(value: fhirValidationSettings.ProfilePackageServiceUrl.OriginalString),
            });
        }
        
        if (!string.IsNullOrWhiteSpace(fhirValidationSettings.TerminologyServiceUrl?.OriginalString))
        {
            parameterList.Add(new Parameters.ParameterComponent()
            {
                Name = "TerminologyServiceUrl",
                Value = new FhirUrl(value: fhirValidationSettings.TerminologyServiceUrl.OriginalString),
            });
        }
        
        parameterList.Add(new Parameters.ParameterComponent()
        {
            Name = "ValidateOnUpdate",
            Value = new FhirBoolean(value: fhirValidationSettings.ValidateOnUpdate),
        });
        
        parameterList.Add(new Parameters.ParameterComponent()
        {
            Name = "ValidateOnCreate",
            Value = new FhirBoolean(value: fhirValidationSettings.ValidateOnCreate),
        });
        
        return new Parameters()
        {
            Meta = new Meta()
            {
                LastUpdated = fhirValidationSettings.LastUpdated,
                VersionId = fhirValidationSettings.VersionId
            },
            Parameter = parameterList
        };
        
    }
}