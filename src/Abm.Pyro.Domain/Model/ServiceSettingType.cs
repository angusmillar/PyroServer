#pragma warning disable CS8618
using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.Model;

public class ServiceSettingType : DbBase
{
  private ServiceSettingType() : base() { }
  
  public ServiceSettingType(
    ServiceSettingTypeId serviceSettingTypeId,
    string name)
  {
    ServiceSettingTypeId = serviceSettingTypeId;
    Name = name;
  }

  public ServiceSettingTypeId ServiceSettingTypeId { get; set; }
  public string Name { get; set; }
}
