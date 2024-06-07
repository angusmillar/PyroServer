using Abm.Pyro.Domain.Model;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirSupport;

public class DateTimeIndexSupport(IFhirDateTimeFactory fhirDateTimeFactory, IFhirDateTimeSupport fhirDateTimeSupport) : IDateTimeIndexSupport
{
  public IndexDateTime? GetDateTimeIndex(Date value, int searchParameterId)
  {
    string? stringDateTime = value.ToString();
    if (stringDateTime is null)
    {
      return null;
    }

    if (!fhirDateTimeFactory.TryParse(stringDateTime, out DateTimeWithPrecision? fhirDateTime, out string? errorMessage))
    {
      return null;      
    }
    
    if (fhirDateTime is null)
    {
      throw new NullReferenceException(nameof(fhirDateTime));
    }

    return new IndexDateTime(
      indexDateTimeId: null,
      resourceStoreId: null,
      resourceStore: null,
      searchParameterStoreId: searchParameterId,
      searchParameterStore: null,
      lowUtc: fhirDateTime!.DateTime,
      highUtc: fhirDateTimeSupport.IndexSettingCalculateHighDateTimeForRange(fhirDateTime.DateTime, fhirDateTime.Precision));

  }

  public IndexDateTime? GetDateTimeIndex(FhirDateTime value, int searchParameterId)
  {
    if (!fhirDateTimeFactory.TryParse(value.Value, out DateTimeWithPrecision? fhirDateTime, out string? errorMessage))
    {
      return null;  
    }
    
    if (fhirDateTime is null)
    {
      throw new NullReferenceException(nameof(fhirDateTime));
    }

    return new IndexDateTime(
      indexDateTimeId: null,
      resourceStoreId: null,
      resourceStore: null,
      searchParameterStoreId: searchParameterId,
      searchParameterStore: null,
      lowUtc: fhirDateTime.DateTime,
      highUtc: fhirDateTimeSupport.IndexSettingCalculateHighDateTimeForRange(fhirDateTime.DateTime, fhirDateTime.Precision));
  }

  public IndexDateTime? GetDateTimeIndex(Instant value, int searchParameterId)
  {
    string? stringDateTime = value.ToString();
    if (stringDateTime is null)
    {
      return null;
    }

    if (!fhirDateTimeFactory.TryParse(stringDateTime, out DateTimeWithPrecision? fhirDateTime, out string? _))
    {
      return null;  
    }
    if (fhirDateTime is null)
    {
      throw new NullReferenceException(nameof(fhirDateTime));
    }

    return new IndexDateTime(
      indexDateTimeId: null,
      resourceStoreId: null,
      resourceStore: null,
      searchParameterStoreId: searchParameterId,
      searchParameterStore: null,
      lowUtc: fhirDateTime!.DateTime,
      highUtc: fhirDateTimeSupport.IndexSettingCalculateHighDateTimeForRange(fhirDateTime.DateTime, fhirDateTime.Precision));
  }

  public IndexDateTime? GetDateTimeIndex(Period value, int searchParameterId)
  {
    DateTimeWithPrecision? startFhirDateTime = null;
    if (!String.IsNullOrWhiteSpace(value.StartElement?.ToString()))
    {
      fhirDateTimeFactory.TryParse(value.StartElement.ToString()!, out startFhirDateTime, out string? errorMessage);
    }

    DateTimeWithPrecision? endFhirDateTime = null;
    if (!String.IsNullOrWhiteSpace(value.EndElement?.ToString()))
    {
      fhirDateTimeFactory.TryParse(value.EndElement.ToString()!, out endFhirDateTime, out string? errorMessage);
    }

    if (startFhirDateTime?.DateTime is not null && endFhirDateTime?.DateTime is not null)
    {
      return new IndexDateTime(
        indexDateTimeId: null,
        resourceStoreId: null,
        resourceStore: null,
        searchParameterStoreId: searchParameterId,
        searchParameterStore: null,
        lowUtc: startFhirDateTime!.DateTime,
        highUtc: fhirDateTimeSupport.IndexSettingCalculateHighDateTimeForRange(endFhirDateTime!.DateTime, endFhirDateTime!.Precision));
    }

    if (startFhirDateTime?.DateTime is not null)
    {
      return new IndexDateTime(
        indexDateTimeId: null,
        resourceStoreId: null,
        resourceStore: null,
        searchParameterStoreId: searchParameterId,
        searchParameterStore: null,
        lowUtc: startFhirDateTime!.DateTime,
        highUtc: null);
    }

    if (endFhirDateTime?.DateTime is not null)
    {
      return new IndexDateTime(
        indexDateTimeId: null,
        resourceStoreId: null,
        resourceStore: null,
        searchParameterStoreId: searchParameterId,
        searchParameterStore: null,
        lowUtc: null,
        highUtc: fhirDateTimeSupport.IndexSettingCalculateHighDateTimeForRange(endFhirDateTime!.DateTime, endFhirDateTime!.Precision));
    }

    return null;
  }

  public IndexDateTime? GetDateTimeIndex(Timing timing, int searchParameterId)
  {
    DateTime? high = null;
    if (timing.Event != null)
    {
      DateTime? low = ResolveTargetEventDateTime(timing, true, searchParameterId);
      if (low != DateTimeOffset.MaxValue.ToUniversalTime())
      {
        decimal targetDuration = ResolveTargetDurationValue(timing);
        Timing.UnitsOfTime? targetUnitsOfTime = null;
        if (targetDuration > decimal.Zero)
        {
          if (timing.Repeat.DurationUnit.HasValue)
            targetUnitsOfTime = timing.Repeat.DurationUnit.Value;
        }

        if (targetDuration > decimal.Zero && targetUnitsOfTime.HasValue)
        {
          high = AddDurationTimeToEvent(ResolveTargetEventDateTime(timing, false, searchParameterId), targetDuration, targetUnitsOfTime.Value);
        }
      }

      var dateTimeIndex = new IndexDateTime(
        indexDateTimeId: null,
        resourceStoreId: null,
        resourceStore: null,
        searchParameterStoreId: searchParameterId,
        searchParameterStore: null,
        lowUtc: low,
        highUtc: high
      );

      return dateTimeIndex;
    }
    return null;

  }
  
  //Check all DateTime values in the list and find the earliest value.        
  private DateTime ResolveTargetEventDateTime(Timing timing, bool targetLowest, int searchParameterId)
  {
    DateTime targetEventDateTime;
    if (targetLowest)
      targetEventDateTime = DateTime.MaxValue.ToUniversalTime();
    else
      targetEventDateTime = DateTime.MinValue.ToUniversalTime();

    foreach (var eventDateTime in timing.EventElement)
    {
      if (!string.IsNullOrWhiteSpace(eventDateTime.Value))
      {
        if (FhirDateTime.IsValidValue(eventDateTime.Value))
        {
          string? partialDateTimeTypeString = eventDateTime.ToString();
          if (partialDateTimeTypeString is not null && fhirDateTimeFactory.TryParse(partialDateTimeTypeString, out var partialDateTimeType, out string? errorMessage))
          {
            if (targetLowest)
            {
              if (targetEventDateTime > partialDateTimeType!.DateTime)
              {
                targetEventDateTime = partialDateTimeType!.DateTime;
              }
            }
            else
            {
              if (targetEventDateTime < partialDateTimeType!.DateTime)
              {
                targetEventDateTime = partialDateTimeType!.DateTime;
              }
            }
          }
        }
      }
    }
    return targetEventDateTime;
  }
  private decimal ResolveTargetDurationValue(Timing timing)
  {
    decimal targetDuration = decimal.Zero;
    decimal durationMax = decimal.Zero;
    decimal duration = decimal.Zero;
    if (timing.Repeat != null)
    {
      if (timing.Repeat.DurationMax != null)
      {
        if (timing.Repeat.DurationMax.HasValue)
        {
          durationMax = timing.Repeat.DurationMax.Value;
        }
      }
      if (durationMax == decimal.Zero)
      {
        if (timing.Repeat.Duration != null)
        {
          if (timing.Repeat.Duration.HasValue)
          {
            duration = timing.Repeat.Duration.Value;
          }
        }
      }
      if (durationMax > decimal.Zero)
      {
        targetDuration = durationMax;
      }
      else if (duration > decimal.Zero)
      {
        targetDuration = duration;
      }
      return targetDuration;
    }
    return decimal.Zero;
  }

  private DateTime AddDurationTimeToEvent(DateTime fromDateTime, decimal targetDuration, Timing.UnitsOfTime targetUnitsOfTime)
  {
    switch (targetUnitsOfTime)
    {
      case Timing.UnitsOfTime.S:
      {
        return fromDateTime.AddSeconds(Convert.ToDouble(targetDuration));
      }
      case Timing.UnitsOfTime.Min:
      {
        return fromDateTime.AddMinutes(Convert.ToDouble(targetDuration));
      }
      case Timing.UnitsOfTime.H:
      {
        return fromDateTime.AddHours(Convert.ToDouble(targetDuration));
      }
      case Timing.UnitsOfTime.D:
      {
        return fromDateTime.AddDays(Convert.ToDouble(targetDuration));
      }
      case Timing.UnitsOfTime.Wk:
      {
        return fromDateTime.AddDays(Convert.ToDouble(targetDuration * 7));
      }
      case Timing.UnitsOfTime.Mo:
      {
        return fromDateTime.AddMonths(Convert.ToInt32(targetDuration));
      }
      case Timing.UnitsOfTime.A:
      {
        return fromDateTime.AddYears(Convert.ToInt32(targetDuration));
      }
      default:
      {
        throw new System.ComponentModel.InvalidEnumArgumentException(targetUnitsOfTime.ToString(), (int)targetUnitsOfTime, typeof(Timing.UnitsOfTime));
      }
    }
  }
}
