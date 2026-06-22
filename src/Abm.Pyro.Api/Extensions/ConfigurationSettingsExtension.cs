using Abm.Pyro.Domain.Configuration;

namespace Abm.Pyro.Api.Extensions;

public static class ConfigurationSettingsExtension
{
    public static IServiceCollection AddConfigurationSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ImplementationSettings>()
            .Bind(configuration.GetSection(ImplementationSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CorsSettings>()
            .Bind(configuration.GetSection(CorsSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<KnownProxiesSettings>()
            .Bind(configuration.GetSection(KnownProxiesSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ServiceBaseUrlSettings>()
            .Bind(configuration.GetSection(ServiceBaseUrlSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ServiceDefaultTimeZoneSettings>()
            .Bind(configuration.GetSection(ServiceDefaultTimeZoneSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PaginationSettings>()
            .Bind(configuration.GetSection(PaginationSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<IncludeRevIncludeSettings>()
            .Bind(configuration.GetSection(IncludeRevIncludeSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<IndexingSettings>()
            .Bind(configuration.GetSection(IndexingSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ResourceEndpointPoliciesSettings>()
            .Bind(configuration.GetSection(ResourceEndpointPoliciesSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TenantSettings>()
            .Bind(configuration.GetSection(TenantSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
