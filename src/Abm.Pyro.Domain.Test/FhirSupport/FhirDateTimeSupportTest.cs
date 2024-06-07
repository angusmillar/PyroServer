using System;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Xunit;

namespace Abm.Pyro.Domain.Test.FhirSupport;

public class FhirDateTimeSupportTest
{
  public readonly static object[][] Data =
  {
    new object[]
    {
      new DateTime(2023,3,1), DateTimePrecision.Day, 
      new DateTime(2023,3,1, 23, 59, 59, 999)
    },
    new object[]
    {
      new DateTime(2023,1,20), DateTimePrecision.Month, 
      new DateTime(2023,2,19, 23, 59, 59, 999)
    },
    new object[]
    {
      new DateTime(2023,1,20), DateTimePrecision.Year, 
      new DateTime(2024,1,19, 23, 59, 59, 999)
    },
    new object[]
    {
      new DateTime(2023,1,20, 10, 30, 25, 000), DateTimePrecision.HourMin, 
      new DateTime(2023,1,20, 10, 31, 24, 999)
    },
    new object[]
    {
      new DateTime(2023,1,20, 10, 30, 25, 000), DateTimePrecision.Sec, 
      new DateTime(2023,1,20, 10, 30, 25, 999)
    },
    new object[]
    {
      new DateTime(2023,1,20, 10, 30, 25, 000), DateTimePrecision.Sec, 
      new DateTime(2023,1,20, 10, 30, 25, 999)
    },
  };
  
  [Theory, MemberData(nameof(Data))]
  public void Success(DateTime low, DateTimePrecision dateTimePrecision, DateTime expectedHigh)
  {
    var fhirDateTimeSupport = new FhirDateTimeSupport();
    DateTime high = fhirDateTimeSupport.IndexSettingCalculateHighDateTimeForRange(low, dateTimePrecision);
    Assert.Equal(expectedHigh, high);
  }

}
