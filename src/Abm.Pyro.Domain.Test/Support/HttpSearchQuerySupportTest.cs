using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Support;
using Xunit;

namespace Abm.Pyro.Domain.Test.Support;

public class HttpSearchQuerySupportTest
{
    
    [Fact]
    public void HttpSearchQuerySupportDecodeUri()
    {
        string queryString = "?parameterOne=https%3A%2F%2FSomeServer.com.au.com.au%2Ffhir%7C123456";
        
        var query = queryString.ParseHttpQuery();
        
        Assert.Equal("https://SomeServer.com.au.com.au/fhir|123456", query["parameterOne"]);
       
    }
    
    [Fact]
    public void HttpSearchQuerySupportDecodeManyOddChars()
    {
        string queryString = "?parameterOne='%40%23%24%25blue%2Blight%20blue";
        
        var query = queryString.ParseHttpQuery();
        
        Assert.Equal("'@#$%blue+light blue", query["parameterOne"]);
       
    }
    
    [Fact]
    public void HttpSearchQuerySupportEncode()
    {
        Dictionary<string, StringValues> searchParameterDictionary = new Dictionary<string, StringValues>();
        searchParameterDictionary.Add("parameterOne", "parameterOneValue");
        searchParameterDictionary.Add("parameterTwo", "parameter Two Value");
        searchParameterDictionary.Add("identifier", "https://SomeServer.com.au.com.au/fhir|123456");
        
        var queryString = searchParameterDictionary.ToEncodedQueryString();
        
        Assert.Equal("?parameterOne=parameterOneValue&parameterTwo=parameter%20Two%20Value&identifier=https%3A%2F%2FSomeServer.com.au.com.au%2Ffhir%7C123456", queryString);
       
    }
}