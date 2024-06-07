using System;
using System.Collections.Generic;
using System.Text;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public abstract class SearchQueryValueBase(bool isMissing)
{
  public bool IsMissing { get; set; } = isMissing;

  public static bool? ParseModifierEqualToMissing(string value)
  {
    if (Boolean.TryParse(value, out bool parsedBooleanValue))
    {
      return parsedBooleanValue;
    }
    return null;
  }
}
