using Abm.Pyro.Domain.Model;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirSupport;

public interface IDateTimeIndexSupport
{
  IndexDateTime? GetDateTimeIndex(Date value, int searchParameterId);
  IndexDateTime? GetDateTimeIndex(Hl7.Fhir.Model.FhirDateTime value, int searchParameterId);
  IndexDateTime? GetDateTimeIndex(Instant value, int searchParameterId);
  IndexDateTime? GetDateTimeIndex(Period value, int searchParameterId);
  IndexDateTime? GetDateTimeIndex(Timing timing, int searchParameterId);
}
