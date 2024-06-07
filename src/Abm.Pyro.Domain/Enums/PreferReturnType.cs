using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum PreferReturnType 
{
  [EnumInfo("minimal", "Minimal")]
  Minimal,
  [EnumInfo("representation", "Representation")]
  Representation,
  [EnumInfo("operationOutcome", "OperationOutcome")]
  OperationOutcome 
};
