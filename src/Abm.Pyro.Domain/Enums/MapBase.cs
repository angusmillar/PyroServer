using Abm.Pyro.Domain.Exceptions;

namespace Abm.Pyro.Domain.Enums;

public abstract class MapBase<TInputEnumType, TReturnEnumType>
  where TReturnEnumType : Enum
  where TInputEnumType : Enum
{
  protected abstract Dictionary<TInputEnumType, TReturnEnumType> ForwardMap { get; }
  protected abstract Dictionary<TReturnEnumType, TInputEnumType> ReverseMap { get; }

  public TReturnEnumType Map(TInputEnumType value)
  {
    if (ForwardMap.ContainsKey(value))
    {
      return ForwardMap[value];
    }
    throw new FhirFatalException(System.Net.HttpStatusCode.InternalServerError, $"Unable to convert {nameof(value)} of type {value.GetType().Name} enum to the required return type.");
  }

  public TInputEnumType Map(TReturnEnumType value)
  {
    if (ReverseMap.ContainsKey(value))
    {
      return ReverseMap[value];
    }
    throw new FhirFatalException(System.Net.HttpStatusCode.InternalServerError, $"Unable to convert {nameof(value)} of type {value.GetType().Name} enum to the required return type.");
  }
}
