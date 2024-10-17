using System;
using Microsoft.Extensions.Options;
using Moq;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.ServiceBaseUrlService;

namespace Abm.Pyro.Domain.Test.Factories;

public static class FhirUriFactoryFactory
{
    public static FhirUriFactory GetFhirUriFactory(string serviceBaseUrl)
    {
        Uri serviceBaseUri = new Uri(serviceBaseUrl);
        var primaryServiceBaseUrlServiceMock = new Mock<IPrimaryServiceBaseUrlService>();
        primaryServiceBaseUrlServiceMock.Setup(x => x.GetUri())
            .Returns(serviceBaseUri);

        primaryServiceBaseUrlServiceMock.Setup(x => x.GetUriAsync())
            .ReturnsAsync(serviceBaseUri);
        
        return new FhirUriFactory(primaryServiceBaseUrlServiceMock.Object, new FhirResourceTypeSupport());
    }
}