using System.Net;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abm.Pyro.Domain.TenantService;

public class TenantService(
    ILogger<TenantService> logger,
    IOptions<TenantSettings> tenantSettings,
    IHttpContextAccessor httpContextAccessor) : ITenantService
{
    private Domain.Configuration.Tenant? _scopedTenant;

    public IReadOnlyCollection<Domain.Configuration.Tenant> GetTenantList()
    {
        if (!tenantSettings.Value.TenantList.Any())
        {
            logger.LogCritical("No {TenantSectionName} sections found in the appsettings.json configuration file, " +
                               "there must be at least one Tenant for the application to startup",
                TenantSettings.SectionName);
            
            throw new ApplicationException("No Tenants found in application's configuration file");
        }

        return tenantSettings.Value.TenantList.ToList().AsReadOnly();
    }

    public void SetScopedTenant(Domain.Configuration.Tenant tenant)
    {
        _scopedTenant = tenant;
    }

    public Domain.Configuration.Tenant GetScopedTenant()
    {
        if (_scopedTenant is null && httpContextAccessor.HttpContext?.Request.Path.Value is not null)
        {
            string? requestTenantUrlCode =
                TryGetRequestTenantUrlCode(httpContextAccessor.HttpContext?.Request.Path.Value);

            if (requestTenantUrlCode is null)
            {
                throw new FhirErrorException(httpStatusCode: HttpStatusCode.BadRequest,
                    "Unable to locate a Tenant in request URL");
            }

            if (!TrySetScopedTenantByUrlCode(requestTenantUrlCode))
            {
                throw new FhirErrorException(httpStatusCode: HttpStatusCode.BadRequest, $"The request Tenant: " +
                    $"'{requestTenantUrlCode}' is not a valid Tenant for the application");
            }
        }

        if (_scopedTenant is null)
        {
            return GetDefaultTenant();
        }

        return _scopedTenant;
    }

    private static Domain.Configuration.Tenant GetDefaultTenant()
    {
        return new Domain.Configuration.Tenant()
        {
            Code = "*TenantNotSet*",
            DisplayName = "Tenant has not been set. Are you calling from a background service and forgotten to set it?",
            SqlConnectionStringCode = "*TenantNotSet*"
        };
    }

    private static string? TryGetRequestTenantUrlCode(string? urlPath)
    {
        if (urlPath is null)
        {
            return null;
        }

        string[] pathSplit = urlPath.Split('/');
        if (pathSplit.Length >= 2 && !string.IsNullOrWhiteSpace(pathSplit[1]))
        {
            if (pathSplit[1].Equals(ApplicationConstants.AdminApiPath, StringComparison.OrdinalIgnoreCase) && pathSplit.Length >= 3)
            {
                //It's a call to the admin API not the FHIR Api, so the Tenant is the next segment in the path
                return pathSplit[2];    
            }
            return pathSplit[1];
        }

        return null;
    }

    private bool TrySetScopedTenantByUrlCode(string urlCode)
    {
        Domain.Configuration.Tenant? tenant =
            tenantSettings.Value.TenantList.FirstOrDefault(
                x => x.GetUrlCode().Equals(urlCode, StringComparison.Ordinal));
        if (tenant == null)
        {
            return false;
        }

        _scopedTenant = tenant;
        return true;
    }
}