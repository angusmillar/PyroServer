using Abm.Pyro.Domain.Configuration;
using Microsoft.Extensions.Options;

namespace Abm.Pyro.Domain.Support;

public class DateTimeProvider(IOptions<ServiceDefaultTimeZoneSettings> serviceDefaultTimeZoneSettings) : IDateTimeProvider
{
    public DateTimeOffset Now => GetNow();

    private DateTimeOffset GetNow()
    {
        return DateTimeOffset.Now.ToOffset(serviceDefaultTimeZoneSettings.Value.TimeZoneTimeSpan);
    }
}