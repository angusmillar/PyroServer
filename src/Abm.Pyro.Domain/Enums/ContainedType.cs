using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum ContainedType
{
  [EnumInfo("container", "Container")]
  Container = 0,
  [EnumInfo("contained", "Contained")]
  Contained = 1,   
}
