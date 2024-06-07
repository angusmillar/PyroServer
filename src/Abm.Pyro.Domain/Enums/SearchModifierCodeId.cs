using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum SearchModifierCodeId : int
{
  [EnumInfo("missing", "Equals" )]
  Missing = 1,
  [EnumInfo("exact", "Equals" )]
  Exact = 2,
  [EnumInfo("contains", "Contains" )]
  Contains = 3,
  [EnumInfo("not", "Not" )]
  Not = 4,
  [EnumInfo("text", "Text" )]
  Text = 5,
  [EnumInfo("in", "In" )]
  In = 6,
  [EnumInfo("not-in", "NotIn" )]
  NotIn = 7,
  [EnumInfo("below", "Below" )]
  Below = 8,
  [EnumInfo("above", "Above" )]
  Above = 9,
  [EnumInfo("type", "Type" )]
  Type = 10,
  [EnumInfo("identifier", "Identifier" )]
  Identifier = 11,
  [EnumInfo("ofType", "OfType" )]
  OfType = 12
}
