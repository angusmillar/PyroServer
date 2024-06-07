using Abm.Pyro.Domain.Enums;
namespace Abm.Pyro.Domain.Enums;

public static class StringToEnumMap<TEnumType> where TEnumType : Enum
{
  public static Dictionary<string, TEnumType> GetDictionary()
  {
    return Enum.GetValues(typeof(TEnumType)).Cast<TEnumType>().ToDictionary(x => x.GetCode(), y => y);            
  }
}
