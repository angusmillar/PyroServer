using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum SearchParamType
{
  [EnumInfo("number", "Number" )]
  Number = 1,
  [EnumInfo("date", "Date" )]
  Date = 2,
  [EnumInfo("string", "String" )]
  String = 3,
  [EnumInfo("token", "Token" )]
  Token = 4,
  [EnumInfo("reference", "Reference" )]
  Reference = 5,
  [EnumInfo("composite", "Composite" )]
  Composite = 6,
  [EnumInfo("quantity", "Quantity" )]
  Quantity = 7,
  [EnumInfo("uri", "Uri" )]
  Uri = 8,
  [EnumInfo("special", "Special" )]
  Special = 9
}
