using System.Text;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.Support;

public static class HttpSearchQuerySupport
{
    private const char ValuePairDelimiter = '&';
    private const char ValueDelimiter = '=';
    private const char QueryDelimiter = '?';
    public static Dictionary<string, StringValues> ParseHttpQuery(this string? queryString)
    {
        Dictionary<string, StringValues> searchParameterDictionary = new Dictionary<string, StringValues>();
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return searchParameterDictionary;
        }
        queryString = queryString.TrimStart(QueryDelimiter);
        foreach (string keyValuePair in queryString.Split(ValuePairDelimiter))
        {
            string[] keyValuePairArray = keyValuePair.Split(ValueDelimiter);
            string key = Uri.UnescapeDataString(keyValuePairArray[0].Trim());
            string value = Uri.UnescapeDataString(keyValuePairArray[1].Trim());
            if (searchParameterDictionary.TryGetValue(key, out var valueTarget))
            {
                searchParameterDictionary[key] = StringValues.Concat(valueTarget, new StringValues(value));
            }
            else
            {
                searchParameterDictionary.Add(key, value);
            }
        }

        return searchParameterDictionary;
    }
    
    public static string ToEncodedQueryString(this Dictionary<string, StringValues> queryDictionary)
    {
        StringBuilder queryStringBuilder = new StringBuilder();
        queryStringBuilder.Append(QueryDelimiter);
        for (var i = 0; i < queryDictionary.Count; i++)
        {
            foreach (var value in queryDictionary.ElementAt(i).Value)
            {
                queryStringBuilder.Append(Uri.EscapeDataString(queryDictionary.ElementAt(i).Key));
                queryStringBuilder.Append(ValueDelimiter);
                queryStringBuilder.Append(Uri.EscapeDataString(value ?? string.Empty));
                if (i != queryDictionary.Count -1)
                {
                    queryStringBuilder.Append(ValuePairDelimiter);
                }
            }
        }

        return queryStringBuilder.ToString();
    }
    
    public static string ToQueryHumanReadableQueryString(this string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return string.Empty;
        }
        StringBuilder queryStringBuilder = new StringBuilder();
        foreach (var valuePair in ParseHttpQuery(queryString))
        {
            foreach (var value in valuePair.Value)
            {
                queryStringBuilder.Append($"{valuePair.Key}{ValueDelimiter}{value}&");    
            }
        }
        return $"?{queryStringBuilder}".TrimEnd('&');
    }
}