namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirDateTimeFactory
{
  bool TryParse(string fhirDateTimeString, out DateTimeWithPrecision? fhirDateTime, out string? errorMessage);
  
}
