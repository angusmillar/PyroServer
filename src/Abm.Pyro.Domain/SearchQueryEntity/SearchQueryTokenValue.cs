using System;
using System.Collections.Generic;
using System.Text;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryTokenValue(bool IsMissing, SearchQueryTokenValue.TokenSearchType? searchType, string? code, string? system)
  : SearchQueryValueBase(IsMissing)
{
  public enum TokenSearchType
  {
    /// <summary>
    /// [parameter]=[code]: the value of [code] matches a Coding.code or 
    /// Identifier.value irrespective of the value of the system property
    /// </summary>
    MatchCodeOnly,
    /// <summary>
    /// [parameter]=[system]|: any element where the value of [system] 
    /// matches the system property of the Identifier or Coding
    /// </summary>
    MatchSystemOnly,
    /// <summary>
    /// [parameter]=[system]|[code]: the value of [code] matches a Coding.code 
    /// or Identifier.value, and the value of [system] matches the system property 
    /// of the Identifier or Coding
    /// </summary>
    MatchCodeAndSystem,
    /// <summary>
    /// [parameter]=|[code]: the value of [code] matches a Coding.code or 
    /// Identifier.value, and the Coding/Identifier has no system property
    /// </summary>
    MatchCodeWithNullSystem
  };

  public TokenSearchType? SearchType { get; set; } = searchType;
  public string? Code { get; set; } = code;
  public string? System { get; set; } = system;
}
