using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum OperationScope
{
  [EnumInfo("Base", "Base")] Base,
  [EnumInfo("Resource", "Resource")] Resource,
  [EnumInfo("Instance", "Instance")] Instance
}
