using System.ComponentModel;

namespace Abm.Pyro.Domain.Configuration;
using System.ComponentModel.DataAnnotations;
public class ServiceBaseUrlSettings
{
  public const string SectionName = "ServiceBaseUrl";
  
  [DisplayName("Service Base Url")]
  [Required]
  public required Uri Url { get; init; }
}
