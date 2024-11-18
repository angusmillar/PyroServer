using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;

namespace Abm.Pyro.Application.EndpointPolicy;

public class EndpointPolicyService(
    ILogger<EndpointPolicyService> logger,
    IEndpointPolicyRules endpointPolicyRules,
    IFhirResourceNameSupport fhirResourceNameSupport,
    IOptions<ResourceEndpointPoliciesSettings> resourceEndpointPolicySettings
) : IEndpointPolicyService
{
    
    public EndpointPolicy GetDefaultEndpointPolicy(string tenantCode)
    {
        if (!endpointPolicyRules.IsEndpointPolicyConfigurationValid)
        {
            LogEndpointPolicyConfigurationIsInValid(tenantCode);
            return GetDenyAllEndpointPolicy();
        }

        return endpointPolicyRules.AllTenantEndpointPolicyDictionary[tenantCode].DefaultPolicy;
        
    }

    public EndpointPolicy GetEndpointPolicy(string tenantCode, string endpointName)
    {
        if (!endpointPolicyRules.IsEndpointPolicyConfigurationValid)
        {
            LogEndpointPolicyConfigurationIsInValid(tenantCode);
            return GetDenyAllEndpointPolicy();
        }

        return endpointPolicyRules.AllTenantEndpointPolicyDictionary[tenantCode]
            .EndpointPolicyDictionary
            .GetValueOrDefault(endpointName, endpointPolicyRules.AllTenantEndpointPolicyDictionary[tenantCode].DefaultPolicy);
        
    }

    public bool ValidateConfiguration(
        string tenantCode,
        CancellationToken cancellationToken)
    {
        endpointPolicyRules.IsEndpointPolicyConfigurationValid = true;
        ValidateTenantDefaultPolicy(tenantCode);
        ValidateTenant(tenantCode);
        
        if (!endpointPolicyRules.IsEndpointPolicyConfigurationValid)
        {
            LogEndpointPolicyConfigurationIsInValid(tenantCode);
        }
        
        ResourceEndpointTenant tenantPolicies = resourceEndpointPolicySettings.Value.Tenants.First(x => 
            x.TenantCode.Equals(tenantCode, StringComparison.OrdinalIgnoreCase));
        
        int resourceEndpointPolicyCounter = 0;
        foreach (ResourceEndpointPolicyMap resourceEndpointPolicy in tenantPolicies.Enforce)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                endpointPolicyRules.IsEndpointPolicyConfigurationValid = false;
                return endpointPolicyRules.IsEndpointPolicyConfigurationValid;
            }

            resourceEndpointPolicyCounter++;
            ValidateEndpointNames(tenantCode, resourceEndpointPolicy, resourceEndpointPolicyCounter);
            ValidatePolicies(tenantCode, resourceEndpointPolicy, resourceEndpointPolicyCounter);
        }

        if (!endpointPolicyRules.IsEndpointPolicyConfigurationValid)
        {
            LogEndpointPolicyConfigurationIsInValid(tenantCode);
        }

        return endpointPolicyRules.IsEndpointPolicyConfigurationValid;
    }

    public void PrimeEndpointPolicies(string tenantCode)
    {
        if (!endpointPolicyRules.IsEndpointPolicyConfigurationValid)
        {
            LogEndpointPolicyConfigurationIsInValid(tenantCode);
            return;
        }

        var defaultEndpointPolicy = LoadTenantDefaultEndpointPolicy(tenantCode);
        
        TenantEndpointPolicyRules tenantEndpointPolicyRules = new TenantEndpointPolicyRules(
            TenantCode: tenantCode,
            DefaultPolicy: defaultEndpointPolicy, 
            EndpointPolicyDictionary: LoadTenantEndpointPolicyDictionary(tenantCode, defaultEndpointPolicy));
        
        endpointPolicyRules.AllTenantEndpointPolicyDictionary.Add(tenantCode, tenantEndpointPolicyRules);
        
    }

    private void LogEndpointPolicyConfigurationIsInValid(string tenantCode)
    {
        logger.LogCritical("The appsettings.json {Section} configuration for Tenant {Tenant} was found to be invalid. " +
                           "A system default {DenyAll} policy has been loaded by the system which will prevent access to all endpoints. " +
                           "Please review the server's startup logs for the specific configuration issues",
            ResourceEndpointPoliciesSettings.SectionName,
            tenantCode,
            "DenyAll");
    }

    private EndpointPolicy LoadTenantDefaultEndpointPolicy(string tenantCode)
    {
        ResourceEndpointPolicy? tenantDefaultPolicy =
            resourceEndpointPolicySettings.Value.Policies.FirstOrDefault(x =>
                x.PolicyCode.Equals(resourceEndpointPolicySettings.Value.Tenants.First(x => 
                    x.TenantCode.Equals(tenantCode, StringComparison.OrdinalIgnoreCase))
                    .DefaultPolicy, StringComparison.OrdinalIgnoreCase));

        ArgumentNullException.ThrowIfNull(tenantDefaultPolicy);

        return new EndpointPolicy(
            AllowCreate: tenantDefaultPolicy.AllowCreate,
            AllowRead: tenantDefaultPolicy.AllowRead,
            AllowUpdate: tenantDefaultPolicy.AllowUpdate,
            AllowDelete: tenantDefaultPolicy.AllowDelete,
            AllowSearch: tenantDefaultPolicy.AllowSearch,
            AllowVersionRead: tenantDefaultPolicy.AllowVersionRead,
            AllowHistory: tenantDefaultPolicy.AllowHistory,
            AllowConditionalCreate: tenantDefaultPolicy.AllowConditionalCreate,
            AllowConditionalUpdate: tenantDefaultPolicy.AllowConditionalUpdate,
            AllowConditionalDelete: tenantDefaultPolicy.AllowConditionalDelete,
            AllowBaseTransaction: tenantDefaultPolicy.AllowBaseTransaction,
            AllowBaseBatch: tenantDefaultPolicy.AllowBaseBatch,
            AllowBaseMetadata: tenantDefaultPolicy.AllowBaseMetadata,
            AllowBaseHistory: tenantDefaultPolicy.AllowBaseHistory);
    }

    private Dictionary<string, EndpointPolicy> LoadTenantEndpointPolicyDictionary(string tenantCode, EndpointPolicy defaultEndpointPolicy)
    {

        var tenantEndpointPolicyDictionary = new Dictionary<string, EndpointPolicy>();
        foreach (FhirResourceTypeId resourceType in Enum.GetValues(typeof(FhirResourceTypeId)))
        {
            string resourceName = resourceType.GetCode();
            
            var resourceEndpointTenant = resourceEndpointPolicySettings.Value.Tenants.First(x => 
                x.TenantCode.Equals(tenantCode, StringComparison.OrdinalIgnoreCase));
            
            IEnumerable<ResourceEndpointPolicyMap> resourceTypeEndpointPolicyMap =
                resourceEndpointTenant.Enforce.Where(x
                    => x.Endpoints.Contains(resourceName)).ToList();

            if (resourceTypeEndpointPolicyMap.Any())
            {
                IEnumerable<ResourceEndpointPolicy> enforceableResourceTypeEndpointPolicyList =
                    resourceEndpointPolicySettings.Value.Policies.Where(x
                        => resourceTypeEndpointPolicyMap.Any(c => c.Policies.Contains(x.PolicyCode)));

                bool allowCreate = defaultEndpointPolicy.AllowCreate;
                bool allowRead = defaultEndpointPolicy.AllowRead;
                bool allowUpdate = defaultEndpointPolicy.AllowUpdate;
                bool allowDelete = defaultEndpointPolicy.AllowDelete;
                bool allowSearch = defaultEndpointPolicy.AllowSearch;
                bool allowVersionRead = defaultEndpointPolicy.AllowVersionRead;
                bool allowHistory = defaultEndpointPolicy.AllowHistory;
                bool allowConditionalCreate = defaultEndpointPolicy.AllowConditionalCreate;
                bool allowConditionalUpdate = defaultEndpointPolicy.AllowConditionalUpdate;
                bool allowConditionalDelete = defaultEndpointPolicy.AllowConditionalDelete;
                bool allowBaseTransaction = defaultEndpointPolicy.AllowBaseTransaction;
                bool allowBaseBatch = defaultEndpointPolicy.AllowBaseBatch;
                bool allowBaseMetadata = defaultEndpointPolicy.AllowBaseMetadata;
                bool allowBaseHistory = defaultEndpointPolicy.AllowBaseHistory;

                foreach (var enforceableEndpointPolicy in enforceableResourceTypeEndpointPolicyList)
                {
                    //We only overwrite the default policy settings where a false is found; false always overrides true 
                    allowCreate = OnlySetIfFalse(enforceableEndpointPolicy.AllowCreate, allowCreate);
                    allowRead = OnlySetIfFalse(enforceableEndpointPolicy.AllowRead, allowRead);
                    allowUpdate = OnlySetIfFalse(enforceableEndpointPolicy.AllowUpdate, allowUpdate);
                    allowDelete = OnlySetIfFalse(enforceableEndpointPolicy.AllowDelete, allowDelete);
                    allowSearch = OnlySetIfFalse(enforceableEndpointPolicy.AllowSearch, allowSearch);
                    allowVersionRead = OnlySetIfFalse(enforceableEndpointPolicy.AllowVersionRead, allowVersionRead);
                    allowHistory = OnlySetIfFalse(enforceableEndpointPolicy.AllowHistory, allowHistory);
                    allowConditionalCreate = OnlySetIfFalse(enforceableEndpointPolicy.AllowConditionalCreate,
                        allowConditionalCreate);
                    allowConditionalUpdate = OnlySetIfFalse(enforceableEndpointPolicy.AllowConditionalUpdate,
                        allowConditionalUpdate);
                    allowConditionalDelete = OnlySetIfFalse(enforceableEndpointPolicy.AllowConditionalDelete,
                        allowConditionalDelete);
                    allowBaseTransaction = OnlySetIfFalse(enforceableEndpointPolicy.AllowBaseTransaction,
                        allowBaseTransaction);
                    allowBaseBatch = OnlySetIfFalse(enforceableEndpointPolicy.AllowBaseTransaction, allowBaseBatch);
                    allowBaseMetadata =
                        OnlySetIfFalse(enforceableEndpointPolicy.AllowBaseTransaction, allowBaseMetadata);
                    allowBaseHistory = OnlySetIfFalse(enforceableEndpointPolicy.AllowBaseHistory, allowBaseHistory);
                }

                tenantEndpointPolicyDictionary.Add(resourceName, new EndpointPolicy(
                    AllowCreate: allowCreate,
                    AllowRead: allowRead,
                    AllowUpdate: allowUpdate,
                    AllowDelete: allowDelete,
                    AllowSearch: allowSearch,
                    AllowVersionRead: allowVersionRead,
                    AllowHistory: allowHistory,
                    AllowConditionalCreate: allowConditionalCreate,
                    AllowConditionalUpdate: allowConditionalUpdate,
                    AllowConditionalDelete: allowConditionalDelete,
                    AllowBaseTransaction: allowBaseTransaction,
                    AllowBaseBatch: allowBaseBatch,
                    AllowBaseMetadata: allowBaseMetadata,
                    AllowBaseHistory: allowBaseHistory));
                
            }
        }

        return tenantEndpointPolicyDictionary;
    }

    private static bool OnlySetIfFalse(
        bool enforceableValue,
        bool exisingValue)
    {
        if (!enforceableValue)
        {
            return false;
        }

        return exisingValue;
    }

    private void ValidatePolicies(
        string tenantCode,
        ResourceEndpointPolicyMap resourceEndpointPolicyMap,
        int resourceEndpointPolicyCounter)
    {
        foreach (var policyCode in resourceEndpointPolicyMap.Policies)
        {
            var matchedPolicies =
                resourceEndpointPolicySettings.Value.Policies.Where(x =>
                    x.PolicyCode.Equals(policyCode, StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (matchedPolicies.Count == 0)
            {
                logger.LogCritical("The appsettings.json configured {Section1} for Tenant {Tenant} Enforce[{Index}].{Policies1} " +
                                   "lists an invalid PolicyCode of {PolicyCode}. This PolicyCode could not be found in the {Section2}.{Policies2} list. ",
                    ResourceEndpointPoliciesSettings.SectionName,
                    tenantCode,
                    resourceEndpointPolicyCounter,
                    nameof(resourceEndpointPolicySettings.Value.Policies),
                    policyCode,
                    ResourceEndpointPoliciesSettings.SectionName,
                    nameof(resourceEndpointPolicySettings.Value.Policies));
                
                endpointPolicyRules.IsEndpointPolicyConfigurationValid = false;
            }

            if (matchedPolicies.Count() > 1)
            {
                logger.LogCritical("The appsettings.json configured {Section1} for Tenant {Tenant} Enforce[{Index}].{Policies1} " +
                                   "contains duplicate PolicyCodes with the code {DuplicatePolicyCode}. " +
                                   "All PolicyCodes must be unique and are case insensitive. " +
                                   "Only the first PolicyCode in the list will be applied",
                    ResourceEndpointPoliciesSettings.SectionName,
                    tenantCode,
                    resourceEndpointPolicyCounter,
                    nameof(resourceEndpointPolicySettings.Value.Policies),
                    policyCode.ToLower());
                
                endpointPolicyRules.IsEndpointPolicyConfigurationValid = false;
            }
        }
    }

    private void ValidateEndpointNames(
        string tenantCode,
        ResourceEndpointPolicyMap resourceEndpointPolicyMap,
        int resourceEndpointPolicyCounter)
    {
        foreach (var resourceName in resourceEndpointPolicyMap.Endpoints)
        {
            if (!fhirResourceNameSupport.IsResourceTypeString(resourceName))
            {
                logger.LogCritical("The appsettings.json configured {Section} for Tenant {TenantCode} has an invalid Enforce[{Counter}] " +
                                   "instance due to an Endpoints named {ResourceName} which is an invalid Endpoint name. " +
                                   "Ensure you have the spelling and casing correct",
                    ResourceEndpointPoliciesSettings.SectionName,
                    tenantCode,
                    resourceEndpointPolicyCounter,
                    resourceName);
                
                endpointPolicyRules.IsEndpointPolicyConfigurationValid = false;
            }
        }
    }

    private void ValidateTenant(string tenantCode)
    {
        if (!resourceEndpointPolicySettings.Value.Tenants.Any(x => 
                x.TenantCode.Equals(tenantCode, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogCritical("The appsettings.json configured {Section}.{Property} " +
                               "Tenant Code can not be found in the list of {Tenants}" +
                               "The system's default policy which blocks all access to all endpoints has been applied",
                ResourceEndpointPoliciesSettings.SectionName,
                nameof(resourceEndpointPolicySettings.Value.Tenants),
                nameof(resourceEndpointPolicySettings.Value.Policies));

            endpointPolicyRules.IsEndpointPolicyConfigurationValid = false;
            return;
        }
    }

    private void ValidateTenantDefaultPolicy(string tenantCode)
    {
        ResourceEndpointTenant? resourceEndpointTenant = resourceEndpointPolicySettings.Value.Tenants.FirstOrDefault(x =>
            x.TenantCode.Equals(tenantCode, StringComparison.OrdinalIgnoreCase));

        if (resourceEndpointTenant is null)
        {
            logger.LogCritical(
                "The appsettings.json configured {Section} has no configuration for the the Tenant {TenantCode}",
                ResourceEndpointPoliciesSettings.SectionName,
                tenantCode);
            
            throw new ApplicationException($"The appsettings.json configured {ResourceEndpointPoliciesSettings.SectionName} " +
                                           $"has no configuration for the the Tenant {tenantCode}");
            
        }
        
        if (!resourceEndpointPolicySettings.Value.Policies.Any(x =>
                x.PolicyCode.ToLower().Equals(resourceEndpointTenant.DefaultPolicy.ToLower())))
        {
            logger.LogCritical("The appsettings.json configured {Section}.{Property} " +
                               "DefaultPolicy can not be found in the list of Policies for the tenant code {TenantCode}" +
                               "The system's default policy which blocks all access to all endpoints has been applied",
                ResourceEndpointPoliciesSettings.SectionName,
                nameof(resourceEndpointPolicySettings.Value.Policies),
                tenantCode);

            endpointPolicyRules.IsEndpointPolicyConfigurationValid = false;
        }
    }

    private EndpointPolicy GetDenyAllEndpointPolicy()
    {
        return new EndpointPolicy(
            AllowCreate: false,
            AllowRead: false,
            AllowUpdate: false,
            AllowDelete: false,
            AllowSearch: false,
            AllowVersionRead: false,
            AllowHistory: false,
            AllowConditionalCreate: false,
            AllowConditionalUpdate: false,
            AllowConditionalDelete: false,
            AllowBaseTransaction: false,
            AllowBaseBatch: false,
            AllowBaseMetadata: false,
            AllowBaseHistory: false);
    }
}