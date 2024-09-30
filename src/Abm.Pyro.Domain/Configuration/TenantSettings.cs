using System.ComponentModel.DataAnnotations;
namespace Abm.Pyro.Domain.Configuration;

public sealed class TenantSettings
{
  public const string SectionName = "Tenants";
  
  public required IEnumerable<Tenant> TenantList { get; init; } = new List<Tenant>();
  
}


