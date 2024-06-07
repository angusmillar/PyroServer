using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Abm.Pyro.Domain.FhirSupport;

public class FhirDateTimeFactory(IOptions<ServiceDefaultTimeZoneSettings> serviceDefaultTimeZoneSettings) : IFhirDateTimeFactory
{
  private readonly ServiceDefaultTimeZoneSettings ServiceDefaultTimeZoneSettings = serviceDefaultTimeZoneSettings.Value;
  private const char MinusTimeZoneDelimiter = '-';
  private const char PlusTimeZoneDelimiter = '+';
  private const string TimeDelimiter = "T";
  private const string MilliSecDelimiter = ".";
  private const string HourMinSecDelimiter = ":";
  private const char TermZulu = 'Z';
  private readonly string[] AllowedFormats = new string[] {
                                                            //Without TimeZone Info       
                                                            //The ten millionths of a second in a date and time value.
                                                            "yyyy-MM-ddTHH:mm:ss.fffffff"
                                                            //The millionths of a second in a date and time value.
                                                            ,
                                                            "yyyy-MM-ddTHH:mm:ss.ffffff"
                                                            //The hundred thousandths of a second in a date and time value.
                                                            ,
                                                            "yyyy-MM-ddTHH:mm:ss.fffff"
                                                            //The ten thousandths of a second in a date and time value.
                                                            ,
                                                            "yyyy-MM-ddTHH:mm:ss.ffff"
                                                            //The milliseconds in a date and time value.
                                                            ,
                                                            "yyyy-MM-ddTHH:mm:ss.fff"
                                                            //The hundredths of a second in a date and time value.
                                                            ,
                                                            "yyyy-MM-ddTHH:mm:ss.ff"
                                                            //The tenths of a second in a date and time value.
                                                            ,
                                                            "yyyy-MM-ddTHH:mm:ss.f", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd", "yyyy-MM", "yyyy"
                                                            //With numeric TimeZone Info e.g '+08:00' or '-08:00'      
                                                            ,
                                                            "yyyy-MM-ddTHH:mm:ss.fffffffzzz", "yyyy-MM-ddTHH:mm:ss.ffffffzzz", "yyyy-MM-ddTHH:mm:ss.fffffzzz", "yyyy-MM-ddTHH:mm:ss.ffffzzz", "yyyy-MM-ddTHH:mm:ss.fffzzz", "yyyy-MM-ddTHH:mm:ss.ffzzz", "yyyy-MM-ddTHH:mm:ss.fzzz", "yyyy-MM-ddTHH:mm:sszzz", "yyyy-MM-ddTHH:mmzzz"
                                                            //With Zulu TimeZone e.g 'Z'      
                                                            ,
                                                            "yyyy-MM-ddTHH:mm:ss.fffffffK", "yyyy-MM-ddTHH:mm:ss.ffffffK", "yyyy-MM-ddTHH:mm:ss.fffffK", "yyyy-MM-ddTHH:mm:ss.ffffK", "yyyy-MM-ddTHH:mm:ss.fffK", "yyyy-MM-ddTHH:mm:ss.ffK", "yyyy-MM-ddTHH:mm:ss.fK", "yyyy-MM-ddTHH:mm:ssK", "yyyy-MM-ddTHH:mmK"
                                                          };

  public bool TryParse(string fhirDateTimeString, out DateTimeWithPrecision? fhirDateTime, out string? errorMessage)
  {
    if (String.IsNullOrWhiteSpace(fhirDateTimeString))
    {
      throw new ArgumentNullException($"{nameof(fhirDateTimeString)} cannot be null or an empty string.");
    }

    //I intentionally provide the fhirDateTimeString here and not fhirDateTimeStringWithSec because we want the precision that was provided not the 
    //precision once the seconds have been added, so we want to know if it is HourMin precision not always Sec precision.
    if (!TryParsePrecision(fhirDateTimeString, out DateTimePrecision? dateTimePrecision, out bool? hasTimeZoneInfo))
    {
      fhirDateTime = null;
      errorMessage = $"Unable to parse DateTime using FHIR format rules. Value was: {fhirDateTimeString}, no format could be determined.";
      return false;
    }

    string fhirDateTimeStringWithSec = CorrectByAddingSecondsToHourMinDateTimeWhenItHasNoSeconds(fhirDateTimeString);
    if (!TryParseDateTimeToUniversalTime(fhirDateTimeStringWithSec, hasTimeZoneInfo!.Value, out DateTime? dateTime, out string? message))
    {
      fhirDateTime = null;
      errorMessage = message;
      return false;
    }

    if (!dateTime.HasValue)
    {
      throw new NullReferenceException(nameof(dateTime));
    }
    if (!dateTimePrecision.HasValue)
    {
      throw new NullReferenceException(nameof(dateTimePrecision));
    }

    fhirDateTime = new DateTimeWithPrecision(TruncateToThousandsMilliseconds(dateTime.Value), dateTimePrecision.Value);
    errorMessage = null;
    return true;

  }
  
  private DateTime TruncateToThousandsMilliseconds(DateTime value)
  {
    return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Millisecond);
  }
  private bool TryParsePrecision(string value, out DateTimePrecision? dateTimePrecision, out bool? hasTimeZoneInfo)
  {
    int legnth = value.Length;
    if (value.Split(TimeDelimiter).Length == 1)
    {
      //Must be one of (yyyy-MM-dd, yyyy-MM, yyyy)
      hasTimeZoneInfo = false;
      string[] hourMinSecDelimiterSplit = value.Split(MinusTimeZoneDelimiter);
      if (hourMinSecDelimiterSplit.Length == 1)
      {
        //2020          
        //format = "yyyy";
        dateTimePrecision = DateTimePrecision.Year;
        return true;
      }
      else if (hourMinSecDelimiterSplit.Length == 2)
      {
        //2020-04          
        //format = "yyyy-MM";
        dateTimePrecision = DateTimePrecision.Month;
        return true;

      }
      else if (hourMinSecDelimiterSplit.Length == 3)
      {
        //2020-04-19             
        //format = "yyyy-MM-dd";
        dateTimePrecision = DateTimePrecision.Day;
        return true;
      }
      else
      {
        //format = null;
        dateTimePrecision = null;
        return false;
      }
    }
    else if (value.Split(TimeDelimiter).Length == 2)
    {
      //We have time such as (yyyy-MM-ddTHH:mm, yyyy-MM-ddTHH:mm:ss.ffffffffzzz, yyyy-MM-ddTHH:mm:ss.ffK or many others)
      if (value.EndsWith(TermZulu))
      {
        //We have a Zulu time such as yyyy-MM-ddTHH:mm:ssK
        hasTimeZoneInfo = true;
        if (legnth == 28)
        {
          //2020-04-19T10:30:25.1234567Z            
          //format = "yyyy-MM-ddTHH:mm:ss.fffffffK";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 27)
        {
          //2020-04-19T10:30:25.123456Z                        
          //format = "yyyy-MM-ddTHH:mm:ss.ffffffK";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 26)
        {
          //2020-04-19T10:30:25.12345Z                        
          //format = "yyyy-MM-ddTHH:mm:ss.fffffK";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 25)
        {
          //2020-04-19T10:30:25.1234Z                                    
          //format = "yyyy-MM-ddTHH:mm:ss.ffffK";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 24)
        {
          //2020-04-19T10:30:25.123Z                                                
          //format = "yyyy-MM-ddTHH:mm:ss.fffK";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 23)
        {
          //2020-04-19T10:30:25.12Z                                                            
          //format = "yyyy-MM-ddTHH:mm:ss.ffK";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 22)
        {
          //2020-04-19T10:30:25.1Z             
          //format = "yyyy-MM-ddTHH:mm:ss.fK";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 20)
        {
          //2020-04-19T10:30:25Z                         
          //format = "yyyy-MM-ddTHH:mm:ssK";
          dateTimePrecision = DateTimePrecision.Sec;
          return true;
        }
        else if (legnth == 17)
        {
          //2020-04-19T10:30Z                                     
          //format = "yyyy-MM-ddTHH:mmK";
          dateTimePrecision = DateTimePrecision.HourMin;
          return true;
        }
        else
        {
          //format = null;
          dateTimePrecision = null;
          hasTimeZoneInfo = null;
          return false;
        }
      }
      else if (value.Split(TimeDelimiter)[1].Contains(MinusTimeZoneDelimiter) || value.Split(TimeDelimiter)[1].Contains(PlusTimeZoneDelimiter))
      {
        //We have a numeric timezone on the end such as +08:00 or -08:00
        hasTimeZoneInfo = true;
        if (legnth == 33)
        {
          //2020-04-19T10:30:25.1234567+08:00                                    
          //format = "yyyy-MM-ddTHH:mm:ss.fffffffzzz";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 32)
        {
          //2020-04-19T10:30:25.123456+08:00                                                
          //format = "yyyy-MM-ddTHH:mm:ss.ffffffzzz";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 31)
        {
          //2020-04-19T10:30:25.12345+08:00                                                            
          //format = "yyyy-MM-ddTHH:mm:ss.fffffzzz";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 30)
        {
          //2020-04-19T10:30:25.1234+08:00                     
          //format = "yyyy-MM-ddTHH:mm:ss.ffffzzz";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 29)
        {
          //2020-04-19T10:30:25.123+08:00              
          //format = "yyyy-MM-ddTHH:mm:ss.fffzzz";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 28)
        {
          //2020-04-19T10:30:25.12+08:00                          
          //format = "yyyy-MM-ddTHH:mm:ss.ffzzz";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 27)
        {
          //2020-04-19T10:30:25.1+08:00            
          //format = "yyyy-MM-ddTHH:mm:ss.fzzz";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 25)
        {
          //2020-04-19T10:30:25+08:00                        
          //format = "yyyy-MM-ddTHH:mm:sszzz";
          dateTimePrecision = DateTimePrecision.Sec;
          return true;
        }
        else if (legnth == 22)
        {
          //2020-04-19T10:30+08:00                                    
          //format = "yyyy-MM-ddTHH:mmzzz";
          dateTimePrecision = DateTimePrecision.HourMin;
          return true;
        }
        else
        {
          //format = null;
          dateTimePrecision = null;
          hasTimeZoneInfo = null;
          return false;
        }
      }
      else
      {
        //We have no timezone info
        hasTimeZoneInfo = false;
        if (legnth == 27)
        {
          //2020-04-19T10:30:25.1234567                          
          //format = "yyyy-MM-ddTHH:mm:ss.fffffff";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 26)
        {
          //2020-04-19T10:30:25.123456                                      
          //format = "yyyy-MM-ddTHH:mm:ss.ffffff";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 25)
        {
          //2020-04-19T10:30:25.12345            
          //format = "yyyy-MM-ddTHH:mm:ss.fffff";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 24)
        {
          //2020-04-19T10:30:25.1234            
          //format = "yyyy-MM-ddTHH:mm:ss.ffff";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 23)
        {
          //2020-04-19T10:30:25.123             
          //format = "yyyy-MM-ddTHH:mm:ss.fff";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 22)
        {
          //2020-04-19T10:30:25.12             
          //format = "yyyy-MM-ddTHH:mm:ss.ff";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 21)
        {
          //2020-04-19T10:30:25.1             
          //format = "yyyy-MM-ddTHH:mm:ss.f";
          dateTimePrecision = DateTimePrecision.MilliSec;
          return true;
        }
        else if (legnth == 19)
        {
          //2020-04-19T10:30:25            
          //format = "yyyy-MM-ddTHH:mm:ss";
          dateTimePrecision = DateTimePrecision.Sec;
          return true;
        }
        else if (legnth == 16)
        {
          //2020-04-19T10:30                        
          //format = "yyyy-MM-ddTHH:mm";
          dateTimePrecision = DateTimePrecision.HourMin;
          return true;
        }
        else
        {
          //format = null;
          dateTimePrecision = null;
          hasTimeZoneInfo = null;
          return false;
        }
      }
    }
    else
    {
      //format = null;
      dateTimePrecision = null;
      hasTimeZoneInfo = null;
      return false;
    }
  }
  private string CorrectByAddingSecondsToHourMinDateTimeWhenItHasNoSeconds(string fhirDateTime)
  {
    const string secondsToAdd = "00";
    //Correct dateTimes that have no seconds yet do have Hours and Min
    //2017-04-28T18:29:15+10:00   
    //2017-04-28T18:29+10:00      
    //2017-04-28T18:29Z
    //2017-04-28T18:29

    if (fhirDateTime.Length <= 10)
    {
      //it is only a date 2020 or 2020-04 or 2020-04-28
      return fhirDateTime;
    }

    if (fhirDateTime.Contains(MilliSecDelimiter))
    {
      //If it has a '.' then it has to have seconds or the format is incorrect and that will be picked up later
      return fhirDateTime;
    }

    if (fhirDateTime.Split(HourMinSecDelimiter).Length == 4)
    {
      //2017-04-28T18:29:15+10:00
      //If we split on ':' and have 4 parts then we must have seconds or we have an incorrectly formated date that will be picked up later 
      return fhirDateTime;
    }

    if (fhirDateTime.Contains(TimeDelimiter) && fhirDateTime.Split(TimeDelimiter)[1].Contains(MinusTimeZoneDelimiter) && fhirDateTime.Split(HourMinSecDelimiter).Length < 4)
    {
      //2017-04-28T18:29-10:00
      var splitMinus = fhirDateTime.Split(MinusTimeZoneDelimiter);
      return $"{splitMinus[0]}{MinusTimeZoneDelimiter}{splitMinus[1]}{MinusTimeZoneDelimiter}{splitMinus[2]}{HourMinSecDelimiter}{secondsToAdd}{MinusTimeZoneDelimiter}{splitMinus[3]}";

    }
    else if (fhirDateTime.Contains(TimeDelimiter) && fhirDateTime.Split(TimeDelimiter)[1].Contains(PlusTimeZoneDelimiter) && fhirDateTime.Split(HourMinSecDelimiter).Length < 4)
    {
      //2017-04-28T18:29+10:00   
      var splitPlus = fhirDateTime.Split(PlusTimeZoneDelimiter);
      return $"{splitPlus[0]}{HourMinSecDelimiter}{secondsToAdd}{PlusTimeZoneDelimiter}{splitPlus[1]}";
    }
    else if (fhirDateTime.Contains(TermZulu) && fhirDateTime.Split(HourMinSecDelimiter).Length < 3)
    {
      //2017-04-28T18:29Z
      var splitZulu = fhirDateTime.Split(TermZulu);
      return $"{splitZulu[0]}{HourMinSecDelimiter}{secondsToAdd}{TermZulu}";
    }
    else if (fhirDateTime.Split(HourMinSecDelimiter).Length < 3)
    {
      //2017-04-28T18:29
      return $"{fhirDateTime}{HourMinSecDelimiter}{secondsToAdd}";
    }
    else
    {
      return fhirDateTime;
    }

  }

  private bool TryParseDateTimeToUniversalTime(string value, bool hasTimeZone, out DateTime? dateTime, out string? errorMessage)
  {
    if (hasTimeZone)
    {
      //As we have timezone info in the string we can parse straight to DateTimeOffset and then to UniversalTime
      if (DateTimeOffset.TryParseExact(value, AllowedFormats, null, System.Globalization.DateTimeStyles.None, out DateTimeOffset dateTimeOffsetFinal))
      {
        dateTime = dateTimeOffsetFinal.UtcDateTime;
        errorMessage = null;
        return true;
      }

      dateTime = null;
      errorMessage = $"Error parsing a FHIR DateTime with timezone info. Value was: {value}.";
      return false;

    }

    //As we have no timezone info we must first parse to DateTime, then set as local timezone before converting to UniversalTime
    if (DateTime.TryParseExact(value, AllowedFormats, null, System.Globalization.DateTimeStyles.None, out DateTime DateTimeOut))
    {
      DateTimeOffset dateTimeOffsetFinal = new DateTimeOffset(DateTimeOut, ServiceDefaultTimeZoneSettings.TimeZoneTimeSpan);
      dateTime = dateTimeOffsetFinal.UtcDateTime;
      errorMessage = null;
      return true;
    }

    dateTime = null;
    errorMessage = $"Error parsing a FHIR DateTime with no time zone info. Value was {value}.";
    return false;
  }

}
