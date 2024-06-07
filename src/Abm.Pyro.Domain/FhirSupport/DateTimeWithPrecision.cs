using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.FhirSupport;

  public class DateTimeWithPrecision(DateTime dateTime, DateTimePrecision precision)
  {
    public DateTime DateTime { get; set; } = dateTime;
    public DateTimePrecision Precision { get; set; } = precision;
  }

