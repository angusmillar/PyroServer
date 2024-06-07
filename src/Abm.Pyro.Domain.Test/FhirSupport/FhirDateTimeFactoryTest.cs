using System;
using Microsoft.Extensions.Options;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Xunit;

namespace Abm.Pyro.Domain.Test.FhirSupport;

public class FhirDateTimeFactoryTest
{
  [Theory]
  [InlineData("2020-04", DateTimePrecision.Month)]
  [InlineData("2020-04-01", DateTimePrecision.Day)]
  [InlineData("2020-04-01T10:30", DateTimePrecision.HourMin)]
  [InlineData("2020-04-01T10:30:25", DateTimePrecision.Sec)]
  [InlineData("2020-04-01T10:30:25.123", DateTimePrecision.MilliSec)]
  [InlineData("2020-04-01T10:30+05:00", DateTimePrecision.HourMin)]
  [InlineData("2020-04-01T10:30:25+05:00", DateTimePrecision.Sec)]
  [InlineData("2020-04-01T10:30:25.123+05:00", DateTimePrecision.MilliSec)]
  [InlineData("2020-04-01T10:30-05:00", DateTimePrecision.HourMin)]
  [InlineData("2020-04-01T10:30:25-05:00", DateTimePrecision.Sec)]
  [InlineData("2020-04-01T10:30:25.123-05:00", DateTimePrecision.MilliSec)]
  public void Success(string stringDateTime, DateTimePrecision expectedPrecision)
  {
    var serviceDefaultTimeZoneSettingsOptions = Options.Create(
      new ServiceDefaultTimeZoneSettings()
      {
        TimeZoneTimeSpan = TimeSpan.FromHours(10)
      });
    
    var fhirDateTimeFactory = new FhirDateTimeFactory(serviceDefaultTimeZoneSettingsOptions);

    fhirDateTimeFactory.TryParse(stringDateTime, out DateTimeWithPrecision? fhirDateTime, out string? errorMessage);
    Assert.NotNull(fhirDateTime);
    Assert.Equal(expectedPrecision, fhirDateTime!.Precision);
  }
  
  [Theory]
  [InlineData("2020:04")]
  [InlineData("2020:04:01")]
  [InlineData("2020-04-01T10-30+5:00")]
  [InlineData("2020-04-01T10-30+05:0")]
  [InlineData("2020-04-01T10-30+99:00")]
  [InlineData("2020-04-01 10:30:25")]
  [InlineData("2020-04-01T10:30:25 123")]
  public void Fail(string stringDateTime)
  {
    var serviceDefaultTimeZoneSettingsOptions = Options.Create(
      new ServiceDefaultTimeZoneSettings()
      {
        TimeZoneTimeSpan = TimeSpan.FromHours(10)
      });
    
    var fhirDateTimeFactory = new FhirDateTimeFactory(serviceDefaultTimeZoneSettingsOptions);

    fhirDateTimeFactory.TryParse(stringDateTime, out DateTimeWithPrecision? fhirDateTime, out string? errorMessage);
    Assert.Null(fhirDateTime);
    Assert.NotNull(errorMessage);
    Assert.NotEmpty(errorMessage);
  }
}
