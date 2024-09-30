using System.ComponentModel.DataAnnotations;

namespace Abm.Pyro.Domain.Configuration;

public class Tenant
{
    /// <summary>
    /// A code this for the Tenant, not to contain spaces 
    /// </summary>
    [StringLength(maximumLength: 25, MinimumLength = 1) ]
    public required string Code { get; init; }
    
    /// <summary>
    /// A display named  for this for the Tenant, can contain spaces
    /// </summary>
    [StringLength(maximumLength: 50, MinimumLength = 3) ]
    public required string DisplayName { get; init; }
    
    /// <summary>
    /// A display named  for this for the Tenant, can contain spaces
    /// </summary>
    [StringLength(maximumLength: 50, MinimumLength = 1) ]
    public required string SqlConnectionStringCode { get; init; }
    
    /// <summary>
    /// The code becomes the URL path segment that represents this tenant through the FHIR APIs, Admin APIs, all APIs hosted by this service
    /// By default the tenant's Code will be used. However, setting this will override it, must be non-null and not an empty string 
    /// Syntax
    ///     https://AcmeHealth.com/[UrlCode]/Patient/123
    /// Example
    ///     https://AcmeHealth.com/MyTentantA/Patient/123
    /// Where UrlCode = MyTentantA
    /// </summary>
    [StringLength(maximumLength: 25, MinimumLength = 3) ]
    public string? UrlCode { get; init; }

    public string GetUrlCode()
    {
        if (string.IsNullOrWhiteSpace(UrlCode))
        {
            return Code;
        }

        return UrlCode;
    }

}