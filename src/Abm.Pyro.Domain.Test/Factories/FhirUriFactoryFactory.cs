using System;
using Microsoft.Extensions.Options;
using Moq;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.FhirSupport;

namespace Abm.Pyro.Domain.Test.Factories;

public static class FhirUriFactoryFactory
{
    public static FhirUriFactory GetFhirUriFactory(string serviceBaseUrl)
    {
        var serviceBaseUrlSettingsOptionsMock = new Mock<IOptions<ServiceBaseUrlSettings>>();
        serviceBaseUrlSettingsOptionsMock.Setup(x => x.Value)
            .Returns(new ServiceBaseUrlSettings()
            {
                Url = new Uri(serviceBaseUrl)
            });
        
       
        return new FhirUriFactory(serviceBaseUrlSettingsOptionsMock.Object, new FhirResourceTypeSupport());
    }
}