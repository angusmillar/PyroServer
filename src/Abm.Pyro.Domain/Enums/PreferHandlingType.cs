using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum PreferHandlingType 
{
  [EnumInfo("strict", "Strict")]
  Strict,
  [EnumInfo("lenient", "Lenient")]
  Lenient 
};
