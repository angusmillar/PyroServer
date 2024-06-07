using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum PublicationStatusId : int
{
  [EnumInfo("draft", "Draft" )]
  Draft = 1,
  [EnumInfo("active", "Active" )]
  Active = 2,
  [EnumInfo("retired", "Retired" )]
  Retired = 3,
  [EnumInfo("unknown", "Unknown" )]
  Unknown = 4
}
