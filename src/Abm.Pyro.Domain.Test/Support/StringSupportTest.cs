using Abm.Pyro.Domain.Support;
using Xunit;

namespace Abm.Pyro.Domain.Test.Support;

public class StringSupportTest
{

    public class StripHttp
    {
        [Fact]
        public void StripHttpTest()
        {
            const string test1 = "http://SomeServer.com.au/SomeWhere/here";
            string result = test1.StripHttp();
            Assert.Equal("SomeServer.com.au/SomeWhere/here", result);
        }

        [Fact]
        public void StripHttpsTest()
        {
            const string test1 = "https://SomeServer.com.au/SomeWhere/here";
            string result = test1.StripHttp();
            Assert.Equal("SomeServer.com.au/SomeWhere/here", result);
        }
    }
    
    public class IsEqualUri
    {
        [Fact]
        public void UriIsEqual()
        {
            const string uri1 = "http://SomeServer.com.au/SomeWhere/here";
            const string uri2 = "http://sOmEserVer.cOm.aU/SomeWhere/here";
            bool result = uri1.IsEqualUri(uri2);
            Assert.True(result);
        }
        
    }
}