using System;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Xunit;
using FhirDateTime = Hl7.Fhir.Model.FhirDateTime;

namespace Abm.Pyro.Domain.Test.FhirSupport;

public class DateTimeIndexSupportTest
{
  [Fact]
  public void DateSuccess()
  {
    var serviceDefaultTimeZoneSettingsOptions = Options.Create(
      new ServiceDefaultTimeZoneSettings()
      {
        TimeZoneTimeSpan = TimeSpan.FromHours(10)
      });
    
    var fhirDateTimeFactory = new FhirDateTimeFactory(serviceDefaultTimeZoneSettingsOptions);
    var fhirDateTimeSupport = new FhirDateTimeSupport();
    
    var dateTimeIndexSupport = new DateTimeIndexSupport(fhirDateTimeFactory, fhirDateTimeSupport);

    int searchParameterId = 1;
    Date date = new Date("2023");
    
    DateTime expectedLow = new DateTime(2023, 1, 1).ToUniversalTime();
    DateTime expectedHigh = new DateTime(2023, 12, 31, 23, 59, 59, 999).ToUniversalTime();

    IndexDateTime? dateTimeIndex = dateTimeIndexSupport.GetDateTimeIndex(date, searchParameterId);
    
    Assert.NotNull(dateTimeIndex);
    Assert.Equal(expectedLow, dateTimeIndex!.LowUtc);
    Assert.Equal(expectedHigh, dateTimeIndex!.HighUtc);

  }
  
  [Fact]
  public void FhirDateTimeSuccess()
  {
    var serviceDefaultTimeZoneSettingsOptions = Options.Create(
      new ServiceDefaultTimeZoneSettings()
      {
        TimeZoneTimeSpan = TimeSpan.FromHours(10)
      });
    
    var fhirDateTimeFactory = new FhirDateTimeFactory(serviceDefaultTimeZoneSettingsOptions);
    var fhirDateTimeSupport = new FhirDateTimeSupport();
    
    var dateTimeIndexSupport = new DateTimeIndexSupport(fhirDateTimeFactory, fhirDateTimeSupport);

    int searchParameterId = 1;
    Hl7.Fhir.Model.FhirDateTime fhirDateTime = new Hl7.Fhir.Model.FhirDateTime(2023);
    
    DateTime expectedLow = new DateTime(2023, 1, 1).ToUniversalTime();
    DateTime expectedHigh = new DateTime(2023, 12, 31, 23, 59, 59, 999).ToUniversalTime();

    IndexDateTime? dateTimeIndex = dateTimeIndexSupport.GetDateTimeIndex(fhirDateTime, searchParameterId);
    
    Assert.NotNull(dateTimeIndex);
    Assert.Equal(expectedLow, dateTimeIndex!.LowUtc);
    Assert.Equal(expectedHigh, dateTimeIndex!.HighUtc);

  }
  
  [Fact]
  public void InstantSuccess()
  {
    var serviceDefaultTimeZoneSettingsOptions = Options.Create(
      new ServiceDefaultTimeZoneSettings()
      {
        TimeZoneTimeSpan = TimeSpan.FromHours(10)
      });
    
    var fhirDateTimeFactory = new FhirDateTimeFactory(serviceDefaultTimeZoneSettingsOptions);
    var fhirDateTimeSupport = new FhirDateTimeSupport();
    
    var dateTimeIndexSupport = new DateTimeIndexSupport(fhirDateTimeFactory, fhirDateTimeSupport);

    int searchParameterId = 1;
    var instantDateTimeOffSet = new DateTimeOffset(2023, 2, 15, 10, 30, 25, 000, TimeSpan.FromHours(10));
    Instant fhirDateTime = new Instant(instantDateTimeOffSet);
    
    DateTime expectedLow = instantDateTimeOffSet.UtcDateTime;
    DateTime expectedHigh = new DateTimeOffset(2023, 2, 15, 10, 30, 25, 999, TimeSpan.FromHours(10)).UtcDateTime;

    IndexDateTime? dateTimeIndex = dateTimeIndexSupport.GetDateTimeIndex(fhirDateTime, searchParameterId);
    
    Assert.NotNull(dateTimeIndex);
    Assert.Equal(expectedLow, dateTimeIndex!.LowUtc);
    Assert.Equal(expectedHigh, dateTimeIndex!.HighUtc);
  }
  
  [Fact]
  public void PeriodSuccess()
  {
    var serviceDefaultTimeZoneSettingsOptions = Options.Create(
      new ServiceDefaultTimeZoneSettings()
      {
        TimeZoneTimeSpan = TimeSpan.FromHours(10)
      });
    
    var fhirDateTimeFactory = new FhirDateTimeFactory(serviceDefaultTimeZoneSettingsOptions);
    var fhirDateTimeSupport = new FhirDateTimeSupport();
    
    var dateTimeIndexSupport = new DateTimeIndexSupport(fhirDateTimeFactory, fhirDateTimeSupport);

    int searchParameterId = 1;
    Hl7.Fhir.Model.FhirDateTime fhirDateTimeStart = new Hl7.Fhir.Model.FhirDateTime(2023, 02, 5, 10, 30, 25, TimeSpan.FromHours(10));
    Hl7.Fhir.Model.FhirDateTime fhirDateTimeEnd = new Hl7.Fhir.Model.FhirDateTime(2023, 02, 10, 10, 30, 25, TimeSpan.FromHours(10));
    
    Period period = new Period(fhirDateTimeStart, fhirDateTimeEnd);
    
    DateTime expectedLow = new DateTime(2023, 02, 5, 10, 30, 25, 000).ToUniversalTime();
    DateTime expectedHigh = new DateTime(2023, 02, 10, 10, 30, 25, 999).ToUniversalTime();
    
    
    IndexDateTime? dateTimeIndex = dateTimeIndexSupport.GetDateTimeIndex(period, searchParameterId);
    
    Assert.NotNull(dateTimeIndex);
    Assert.Equal(expectedLow, dateTimeIndex!.LowUtc);
    Assert.Equal(expectedHigh, dateTimeIndex!.HighUtc);
  }
  
  [Fact]
  public void TimingSuccess()
  {
    var serviceDefaultTimeZoneSettingsOptions = Options.Create(
      new ServiceDefaultTimeZoneSettings()
      {
        TimeZoneTimeSpan = TimeSpan.FromHours(10)
      });
    
    var fhirDateTimeFactory = new FhirDateTimeFactory(serviceDefaultTimeZoneSettingsOptions);
    var fhirDateTimeSupport = new FhirDateTimeSupport();
    
    var dateTimeIndexSupport = new DateTimeIndexSupport(fhirDateTimeFactory, fhirDateTimeSupport);

    int searchParameterId = 1;
    Hl7.Fhir.Model.FhirDateTime eventStartDateTime1 = new Hl7.Fhir.Model.FhirDateTime(2023, 02, 1, 08, 31, 21, TimeSpan.FromHours(10));
    Hl7.Fhir.Model.FhirDateTime eventStartDateTime2 = new Hl7.Fhir.Model.FhirDateTime(2023, 02, 2, 09, 32, 22, TimeSpan.FromHours(10));
    Hl7.Fhir.Model.FhirDateTime eventStartDateTime3 = new Hl7.Fhir.Model.FhirDateTime(2023, 02, 3, 10, 33, 23, TimeSpan.FromHours(10));
    
    Timing timing = new Timing();
    timing.EventElement = new List<FhirDateTime>() { eventStartDateTime1, eventStartDateTime2, eventStartDateTime3 };
    timing.Repeat = new Timing.RepeatComponent();
    timing.Repeat.Duration = 1;
    timing.Repeat.DurationUnit = Timing.UnitsOfTime.H;

    DateTime expectedLow = new DateTimeOffset(2023, 02, 1, 08, 31, 21, TimeSpan.FromHours(10)).UtcDateTime;
    DateTime expectedHigh = new DateTime(2023, 02, 3, 11, 33, 23, 000).ToUniversalTime();
    
    
    IndexDateTime? dateTimeIndex = dateTimeIndexSupport.GetDateTimeIndex(timing, searchParameterId);
    
    Assert.NotNull(dateTimeIndex);
    Assert.Equal(expectedLow, dateTimeIndex!.LowUtc);
    Assert.Equal(expectedHigh, dateTimeIndex!.HighUtc);
  }
  
  
}
