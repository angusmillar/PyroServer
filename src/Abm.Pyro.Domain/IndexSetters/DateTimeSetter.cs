using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.IndexSetters;

public class DateTimeSetter(IDateTimeIndexSupport dateTimeIndexSupport, IFhirDateTimeFactory fhirDateTimeFactory) : IDateTimeSetter
{
  private FhirResourceTypeId ResourceType;
  private int SearchParameterId;
  private string? SearchParameterName;

  public IList<IndexDateTime> Set(ITypedElement typedElement, FhirResourceTypeId resourceType, int searchParameterId, string searchParameterName)
  {
    ResourceType = resourceType;
    SearchParameterId = searchParameterId;
    SearchParameterName = searchParameterName;

    if (typedElement is ScopedNode scopedNode && scopedNode.Current is IFhirValueProvider fhirValueProvider)
    {
      if (fhirValueProvider.FhirValue is null)
      {
        throw new NullReferenceException($"FhirValueProvider's FhirValue found to be null for the SearchParameter entity with the database " +
                                         $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                         $"name of: {SearchParameterName}");
      }

      return ProcessFhirDataType(fhirValueProvider.FhirValue);
    }

    if (typedElement.Value is null)
    {
      throw new NullReferenceException($"ITypedElement's Value found to be null for the SearchParameter entity with the database " +
                                       $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                       $"name of: {SearchParameterName}");
    }

    return ProcessPrimitiveDataType(typedElement.Value);

  }

  private IList<IndexDateTime> ProcessPrimitiveDataType(object obj)
  {
    switch (obj)
    {
      default:
        throw new FormatException($"Unknown Primitive DataType: {obj.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");

    }
  }

  private IList<IndexDateTime> ProcessFhirDataType(Base fhirValue)
  {
    switch (fhirValue)
    {
      case Date date:
        return SetDate(date);
      case Period period:
        return SetPeriod(period);
      case FhirDateTime fhirDateTime:
        return SetDateTime(fhirDateTime);
      case FhirString fhirString:
        return SetString(fhirString);
      case Instant instant:
        return SetInstant(instant);
      case Timing timing:
        return SetTiming(timing);
      default:
        throw new FormatException($"Unknown FhirType: {fhirValue.GetType().Name} for the SearchParameter entity with the database " +
                                  $"key of: {SearchParameterId.ToString()} for a resource type of: {ResourceType.GetCode()} and search parameter " +
                                  $"name of: {SearchParameterName}");
    }
  }

  private IList<IndexDateTime> SetTiming(Timing timing)
  {
    IndexDateTime? dateTimeIndex = dateTimeIndexSupport.GetDateTimeIndex(timing, SearchParameterId);
    if (dateTimeIndex is null || (dateTimeIndex.LowUtc is null && dateTimeIndex.HighUtc is null))
    {
      return Array.Empty<IndexDateTime>();
    }

    return new List<IndexDateTime>() { dateTimeIndex };
  }

  private IList<IndexDateTime> SetInstant(Instant instant)
  {
    if (!instant.Value.HasValue)
    {
      return Array.Empty<IndexDateTime>();
    }

    IndexDateTime? dateTimeIndex = dateTimeIndexSupport.GetDateTimeIndex(instant, SearchParameterId);

    if (dateTimeIndex is null || (dateTimeIndex.LowUtc is null && dateTimeIndex!.HighUtc is null))
    {
      return Array.Empty<IndexDateTime>();
    }

    return new List<IndexDateTime>() { dateTimeIndex };
  }

  private IList<IndexDateTime> SetString(FhirString fhirString)
  {
    if (!Hl7.Fhir.Model.Date.IsValidValue(fhirString.Value) && !FhirDateTime.IsValidValue(fhirString.Value))
    {
      return Array.Empty<IndexDateTime>();
    }

    if (!fhirDateTimeFactory.TryParse(fhirString.Value, out DateTimeWithPrecision? dateTimeWithPrecision, out string? _))
    {
      return Array.Empty<IndexDateTime>();
    }

    var fhirDateTime = new FhirDateTime(new DateTimeOffset(dateTimeWithPrecision!.DateTime));
    var dateTimeIndex = dateTimeIndexSupport.GetDateTimeIndex(fhirDateTime, this.SearchParameterId);

    if (dateTimeIndex is null)
    {
      return Array.Empty<IndexDateTime>();
    }

    return new List<IndexDateTime>() { dateTimeIndex };
  }

  private IList<IndexDateTime> SetDateTime(FhirDateTime fhirDateTime)
  {
    if (!FhirDateTime.IsValidValue(fhirDateTime.Value))
    {
      return Array.Empty<IndexDateTime>();
    }

    IndexDateTime? indexDateTime = dateTimeIndexSupport.GetDateTimeIndex(fhirDateTime, this.SearchParameterId);

    if (indexDateTime is null)
    {
      return Array.Empty<IndexDateTime>();
    }

    if (indexDateTime.LowUtc is null && indexDateTime.HighUtc is null)
    {
      return Array.Empty<IndexDateTime>();
    }

    return new List<IndexDateTime>() { indexDateTime };
  }

  private IList<IndexDateTime> SetPeriod(Period period)
  {
    IndexDateTime? indexDateTime = dateTimeIndexSupport.GetDateTimeIndex(period, this.SearchParameterId);

    if (indexDateTime is null)
    {
      return Array.Empty<IndexDateTime>();
    }

    if (indexDateTime.LowUtc is null && indexDateTime.HighUtc is null)
    {
      return Array.Empty<IndexDateTime>();
    }

    return new List<IndexDateTime>() { indexDateTime };
  }

  private IList<IndexDateTime> SetDate(Date date)
  {
    if (!Date.IsValidValue(date.Value))
    {
      return Array.Empty<IndexDateTime>();
    }

    IndexDateTime? indexDateTime = dateTimeIndexSupport.GetDateTimeIndex(date, SearchParameterId);
    if (indexDateTime is null)
    {
      return Array.Empty<IndexDateTime>();
    }

    if (indexDateTime.LowUtc is null && indexDateTime.HighUtc is null)
    {
      return Array.Empty<IndexDateTime>();
    }

    return new List<IndexDateTime>() { indexDateTime };
  }

}
