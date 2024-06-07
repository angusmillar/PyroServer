using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum SummaryType
{
  [EnumInfo("true", "True")]
  True = 0,
  [EnumInfo("text", "Text")]
  Text = 1,
  [EnumInfo("data", "Data")]
  Data = 2,
  [EnumInfo("count", "Count")]
  Count = 3,
  [EnumInfo("false", "False")]
  False = 4
};
