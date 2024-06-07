using BenchmarkDotNet.Attributes;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Domain.Benchmark.Support;

[MemoryDiagnoser]
public class StringSupportBenchmark
{
    
    [Benchmark]
    public string StripHttp()
    {
        const string test1 = "https://SomeServer.com.au/SomeWhere/here";
        const string test2 = "http://SomeServer.com.au/SomeWhere/here";
        test1.StripHttp();
        return test2.StripHttp();
    }
    
}