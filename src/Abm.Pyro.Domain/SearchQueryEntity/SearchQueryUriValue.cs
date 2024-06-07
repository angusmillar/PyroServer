using System;
using System.Collections.Generic;
using System.Text;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryUriValue(bool IsMissing, Uri? Value) : SearchQueryValueBase(IsMissing)
{
  public Uri? Value { get; set; } = Value;
}
