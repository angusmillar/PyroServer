using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum QuantityComparator
{
  [EnumInfo("<", "LessThan")]
  LessThan = 1,
  [EnumInfo("<=", "LessOrEqual")]
  LessOrEqual = 2,
  [EnumInfo(">=", "GreaterOrEqual")]
  GreaterOrEqual = 3,
  [EnumInfo(">", "GreaterThan")]
  GreaterThan = 4
}
