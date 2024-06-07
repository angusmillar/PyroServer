using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirDateTimeSupport
{
  DateTime SearchQueryCalculateHighDateTimeForRange(DateTime lowValue, DateTimePrecision precision);
  DateTime IndexSettingCalculateHighDateTimeForRange(DateTime lowValue, DateTimePrecision precision);
}
