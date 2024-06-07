using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum IncludeType 
{
  [EnumInfo("_include", "Include")]
  Include,
  [EnumInfo("_revinclude", "Revinclude")]
  Revinclude 
};
