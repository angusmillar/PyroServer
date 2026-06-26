using Abm.Pyro.Application.ServiceSettingHandler;
using Abm.Pyro.Domain.ServiceSettingRequest;
using Abm.Pyro.Domain.ServiceSettings;
using Microsoft.AspNetCore.Mvc;
using Hl7.Fhir.Model;
using Abm.Pyro.Application.Dispatcher;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Controllers;

[Route("admin/{tenant}")]
[ApiController]
public class AdminController(
    IFhirValidationSettingsParser fhirValidationSettingsParser,
    IRequestDispatcher requestDispatcher) : ControllerBase
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
        FhirValidationSettingsGetResponse fhirValidationSettingsGetResponse =  await requestDispatcher.Send(new FhirValidationSettingsGetRequest());
        
        return fhirValidationSettingsParser.GetParametersResource(fhirValidationSettingsGetResponse.FhirValidationSettings);
        
    }
    
    // PUT: admin/{tenant}/settings/FhirValidation
    [HttpPut("settings/FhirValidation")]
    public async Task<ActionResult<Resource>> UpdateFhirValidationServiceSetting(Parameters parameters)
    {
        ServiceSettingsParserOutcome<FhirValidationSettings> fhirValidationSettingsOutcome = fhirValidationSettingsParser.GetSettings(parameters);

        if (!fhirValidationSettingsOutcome.Success)
        {
            ArgumentNullException.ThrowIfNull(fhirValidationSettingsOutcome.OperationOutcome);
            return BadRequest(fhirValidationSettingsOutcome.OperationOutcome);
        }
        
        ArgumentNullException.ThrowIfNull(fhirValidationSettingsOutcome.ServiceSettings);
        
        FhirValidationSettingsUpdateResponse fhirValidationSettingsUpdateResponse =  await requestDispatcher.Send(
            new FhirValidationSettingsUpdateRequest(FhirValidationSettings: fhirValidationSettingsOutcome.ServiceSettings));

        return fhirValidationSettingsParser.GetParametersResource(fhirValidationSettingsUpdateResponse.FhirValidationSettings);

    }
}