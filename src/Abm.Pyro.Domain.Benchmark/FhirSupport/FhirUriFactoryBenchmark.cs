using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Options;
using Moq;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.ServiceBaseUrlService;

namespace Abm.Pyro.Domain.Benchmark.FhirSupport;

[MemoryDiagnoser]
public class FhirUriFactoryBenchmark
{
    
    // | Method                   | Mean     | Error     | StdDev    | Ratio | Gen0   | Gen1    | Allocated | Alloc Ratio |
    // |------------------------- |---------:|----------:|----------:|------:|-------:|--------:|----------:|------------:|
    // | FhirUriFactoryRemoteBase | 9.184 us | 0.1986 us | 0.1858 us |  1.00 | 1.3809 | 0.0076  |    8.5 KB |        1.00 |



    [Benchmark(Baseline = true)]
    public static string FhirUriFactoryRemoteBase()
    {
        Uri serviceBaseUri = new Uri("https://SomeFhirServer.com.au/over-here/fhir");
        
        var primaryServiceBaseUrlServiceMock = new Mock<IPrimaryServiceBaseUrlService>();
        primaryServiceBaseUrlServiceMock.Setup(x => x.GetUri())
            .Returns(serviceBaseUri);

        primaryServiceBaseUrlServiceMock.Setup(x => x.GetUriAsync())
            .ReturnsAsync(serviceBaseUri);

        
        IFhirResourceNameSupport fhirResourceNameSupport = new FhirResourceTypeSupport();
        
        // Prepare
        FhirUriFactory fhirUriFactory = new FhirUriFactory(primaryServiceBaseUrlServiceMock.Object, fhirResourceNameSupport);

        string requestUri = "https://SomeOtherFhirServer.com.au/over-here/fhir/Observation/100/_history/300";
        if (fhirUriFactory.TryParse(requestUri,out FhirUri? fhirUri, out _))
        {
            return fhirUri.ResourceName;
        }

        throw new NullReferenceException(nameof(fhirUri));
    }
    
}