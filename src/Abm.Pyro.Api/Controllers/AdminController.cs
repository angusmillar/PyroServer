using MediatR;
using Microsoft.AspNetCore.Mvc;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Repository;

namespace Abm.Pyro.Api.Controllers;

[Route("admin/{tenant}")]
[ApiController]
public class AdminController(ILogger<AdminController> logger, PyroDbContext pyroDbContext ) : ControllerBase
{
  [HttpGet("SearchParameter/{resourceId}/{history}/{historyId}")]
  public async Task<ActionResult> Search(string tenant, string resourceId, string history, string historyId, CancellationToken cancellationToken)
  {
    
    await Task.Delay(2000, cancellationToken);
    logger.LogInformation("Search parameter called : {Tenant}, {ResourceId}, {History}, {HistoryId}", tenant, resourceId, history, historyId);
    
    bool isConnected = await pyroDbContext.Database.CanConnectAsync(cancellationToken);
    
    return Ok();
  }
  
  [HttpGet("Filter")]
  public async Task<ActionResult> Search(string tenant, CancellationToken cancellationToken)
  {
    await Task.Delay(2000, cancellationToken);
    return Ok();
  }
}
