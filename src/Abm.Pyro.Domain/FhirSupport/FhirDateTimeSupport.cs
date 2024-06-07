using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.FhirSupport;

public class FhirDateTimeSupport : IFhirDateTimeSupport
{
  public DateTime SearchQueryCalculateHighDateTimeForRange(DateTime lowValue, DateTimePrecision precision)
  {
    DateTime highDateTime;
    if (precision == DateTimePrecision.Year)
    {
      //To deal with the problem of no time zones on Dates, e.g 2018 or 2018-10 or 2018-10-05 we treat the search as a 36 hour day rather than a 24 hours day
      //When the precision is one on Year, Month or Day. For more find grained precisions such as Hour, Min, Sec we  expected to have the 
      //time zones information supplied either by the calling user or by using the server's default timezone.
      //
      //So to do this I subtract 6 hours from the beginning of the date range 2018-10-05T00:00 and we add 6 hours to the end of the day 2018-10-05T23:59
      //This gives us a 36 hour day range. The idea is that it is better to return more than less for the search.
      //This is a compromise as we really do not know what is meant by a date with no time zone. We can assume the servers default time zone as a starting point
      //but this is only a guess to what the true time zone was for either the supplied search date or the stored FHIR resource dates, when dealing with only date 
      //and no time.  
      //
      //So the range we actually use for this example is not:   
      //  2018-10-05T00:00 to 2018-10-05T23:59 
      //but rather: 
      //  2018-10-04T18:00 to 2018-10-06T05:59 
      //which in a 12hr clock is 04/10/2018 6:00PM to 06/10/2018 6:00AM when the search date was: 05/10/2018
      //Also bare in mind that all date times are converted to UTC Zulu +00:00 time when stored and searched in the database.

      //Work out the normal 24 hour day range low and high
      highDateTime = lowValue.AddYears(1).AddMilliseconds(-1);

      //Subtract 6 hours from the low
      lowValue = lowValue.AddHours(-6);
      //Add 6 hours to the high
      highDateTime = highDateTime.AddHours(6);

    }
    else if (precision == DateTimePrecision.Month)
    {
      //Work out the normal 24 hour day range low and high
      highDateTime = lowValue.AddMonths(1).AddMilliseconds(-1);

      //Subtract 6 hours from the low
      lowValue = lowValue.AddHours(-6);
      //Add 6 hours to the high
      highDateTime = highDateTime.AddHours(6);
    }
    else if (precision == DateTimePrecision.Day)
    {
      //Work out the normal 24 hour day range low and high
      highDateTime = lowValue.AddDays(1).AddMilliseconds(-1);

      //Subtract 6 hours from the low
      lowValue = lowValue.AddHours(-6);
      //Add 6 hours to the high
      highDateTime = highDateTime.AddHours(6);

    }
    else if (precision == DateTimePrecision.HourMin)
    {
      highDateTime = lowValue.AddMinutes(1).AddMilliseconds(-1);
    }
    else if (precision == DateTimePrecision.Sec)
    {
      highDateTime = lowValue.AddSeconds(1).AddMilliseconds(-1);
    }
    else if (precision == DateTimePrecision.MilliSec)
    {
      highDateTime = lowValue.AddMilliseconds(1).AddTicks(-999);
    }
    else
    {
      throw new System.ComponentModel.InvalidEnumArgumentException(precision.ToString(), (int)precision, typeof(DateTimePrecision));
    }
    return highDateTime;
  }

  public DateTime IndexSettingCalculateHighDateTimeForRange(DateTime lowValue, DateTimePrecision precision)
  {
    switch (precision)
    {
      case DateTimePrecision.Year:
        return lowValue.AddYears(1).AddMilliseconds(-1);
      case DateTimePrecision.Month:
        return lowValue.AddMonths(1).AddMilliseconds(-1);
      case DateTimePrecision.Day:
        return lowValue.AddDays(1).AddMilliseconds(-1);
      case DateTimePrecision.HourMin:
        return lowValue.AddMinutes(1).AddMilliseconds(-1);
      case DateTimePrecision.Sec:
        return lowValue.AddSeconds(1).AddMilliseconds(-1);
      case DateTimePrecision.MilliSec:
        return lowValue.AddMilliseconds(1).AddTicks(-999);
      default:
        throw new System.ComponentModel.InvalidEnumArgumentException(precision.GetCode(), (int)precision, typeof(DateTimePrecision));
    }
  }

}
