namespace Abm.Pyro.Domain.Support;

public interface IDateTimeProvider
{
    DateTimeOffset Now { get; }
}