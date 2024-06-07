using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum SearchComparatorId : int
{
  [EnumInfo("eq", "Equals" )]
  Eq = 1,
  [EnumInfo("ne", "NotEquals" )]
  Ne = 2,
  [EnumInfo("gt", "GreaterThan" )]
  Gt = 3,
  [EnumInfo("lt", "LessThan" )]
  Lt = 4,
  [EnumInfo("ge", "GreaterOrEquals" )]
  Ge = 5,
  [EnumInfo("le", "LessOfEqual" )]
  Le = 6,
  [EnumInfo("sa", "StartsAfter" )]
  Sa = 7,
  [EnumInfo("ed", "EndsBefore" )]
  Eb = 8,
  [EnumInfo("ap", "Approximately" )]
  Ap = 9
}
