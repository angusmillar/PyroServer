using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum HttpVerbId : int
{
  [EnumInfo("POST", "Post")]
  Post = 1,
  [EnumInfo("PUT", "Put")]
  Put = 2,
  [EnumInfo("GET", "Get")]
  Get = 3,
  [EnumInfo("DELETE", "Delete")]
  Delete = 4
}
