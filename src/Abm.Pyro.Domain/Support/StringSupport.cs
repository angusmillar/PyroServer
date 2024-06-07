using System.Linq.Expressions;
using System.Net;
using System.Text;
using Hl7.Fhir.Rest;

namespace Abm.Pyro.Domain.Support;

public static class StringSupport
{
  public static string RemoveDiacritics(string text)
  {
    return string.Concat(
      text.Normalize(NormalizationForm.FormD)
          .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) !=
                       System.Globalization.UnicodeCategory.NonSpacingMark)
    ).Normalize(NormalizationForm.FormC);
  }

  public static string ToLowerFast(string text)
  {
    return text.ToLower(System.Globalization.CultureInfo.CurrentCulture);
  }

  public static string ToLowerAndRemoveDiacritics(string text)
  {
    return ToLowerFast(RemoveDiacritics(text));
  }

  public static string ToLowerTrimRemoveDiacriticsTruncate(string text, int truncateMaxLength)
  {
    return ToLowerFast(RemoveDiacritics(text.Trim().Truncate(truncateMaxLength)));
  }

  public static string UppercaseFirst(string s)
  {
    if (string.IsNullOrEmpty(s))
    {
      return string.Empty;
    }
    char[] a = s.ToCharArray();
    a[0] = char.ToUpper(a[0]);
    return new string(a);
  }

  public static string RemoveWhitespace(string text)
  {
    return new string(text.ToCharArray()
                          .Where(c => !Char.IsWhiteSpace(c))
                          .ToArray());
  }

  public static int GetScaleFromDecimal(string value)
  {
    const string decimalPoint = ".";
    if (value.Contains(decimalPoint))
    {
      return value.Length - (value.IndexOf(decimalPoint, StringComparison.Ordinal) + 1);
    }
    else
    {
      return 0;
    }
  }

  public static int GetPrecisionFromDecimal(string value)
  {
    const string decimalPoint = ".";
    if (value.Contains(decimalPoint))
    {
      return value.Length - 1;
    }
    else
    {
      return value.Length;
    }
  }

  public static string Truncate(this string str, int maxLength)
  {
    if (str.Length > maxLength)
    {
      return str.Substring(0, maxLength);
    }
    return str;
  }

  public static bool StringToBoolean(string value)
  {
    value = ToLowerFast(value);
    if ((value == "true") || (value == "yes") || (value == "on") || (value == "1"))
    {
      return true;
    }
    else
    {
      return false;
    }
  }

  public static bool StringIsBoolean(string value)
  {
    value = ToLowerFast(value);
    if ((value == "true") || (value == "yes") || (value == "on") || (value == "1") ||
        (value == "false") || (value == "no") || (value == "off") || (value == "0"))
    {
      return true;
    }
    else
    {
      return false;
    }
  }

  /// <summary>
  /// Returns the string removing http:// or https://
  /// </summary>
  /// <param name="uri"></param>
  /// <returns></returns>
  public static string StripHttp(this string uri)
  {
    const string https = "https://";
    if (uri.StartsWith(https, StringComparison.OrdinalIgnoreCase))
    {
      return uri.Substring(8, uri.Length - 8);
    }
    
    const string http = "http://";
    
    if (uri.StartsWith(http, StringComparison.OrdinalIgnoreCase))
    {
      return uri.Substring(7, uri.Length - 7);
    }
    
    return uri;
  }
  
  /// <summary>
  /// Strips of either https or http and performs a insensative compare between the two uris
  /// </summary>
  /// <param name="left"></param>
  /// <param name="Uri"></param>
  /// <returns></returns>
  public static bool IsEqualUri(this string leftUri, string Uri)
  {
    return string.Equals(leftUri.StripHttp().TrimEnd('/'), Uri.StripHttp().TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
  }
  
  public static string GetPropertyName<TValue>(Expression<Func<TValue>> propertyId)
  {
    return ((MemberExpression)propertyId.Body).Member.Name;
  }
  
  public static string GetEtag(int versionId)
  {
    return $"W/\"{versionId.ToString()}\"";
  }
  
  public static string Display(this HttpStatusCode httpStatusCode)
  {
    return $"{(int)httpStatusCode} {httpStatusCode.ToString()}";
  }
  
  public static string? NullIfEmptyString(this string value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }
    return value;
  }
  
}
