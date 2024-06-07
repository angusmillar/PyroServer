using System.ComponentModel.DataAnnotations;
namespace Abm.Pyro.Domain.Configuration;

public class ServiceDefaultTimeZoneSettings
{
  public const string SectionName = "ServiceDefaultTimeZone";

  [Range(typeof(TimeSpan), "00:00", "23:59")]
  public TimeSpan TimeZoneTimeSpan { get; init; } = new TimeSpan(10, 00, 00);
}
