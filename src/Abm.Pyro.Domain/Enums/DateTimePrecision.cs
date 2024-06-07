using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum DateTimePrecision 
{
  [EnumInfo("Year", "Year")]
  Year,
  [EnumInfo("Month", "Month")]
  Month,
  [EnumInfo("Day", "Day")]
  Day,
  [EnumInfo("HourMin", "HourMin")]
  HourMin,
  [EnumInfo("Sec", "Sec")]
  Sec,
  [EnumInfo("MilliSec", "MilliSec")]
  MilliSec    
};
