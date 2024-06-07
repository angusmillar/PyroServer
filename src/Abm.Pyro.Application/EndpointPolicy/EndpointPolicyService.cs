using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;

namespace Abm.Pyro.Application.EndpointPolicy;

public class EndpointPolicyService(
    ILogger<EndpointPolicyService> logger,
    IFhirResourceNameSupport fhirResourceNameSupport,
    IOptions<ResourceEndpointPolicySettings> resourceEndpointPolicySettings
) : IEndpointPolicyService
{
    private EndpointPolicy? DefaultEndpointPolicy;
    private bool IsEndpointPolicyConfigurationValid;
    private readonly Dictionary<string, EndpointPolicy> EndpointPolicyDictionary = new();

    public EndpointPolicy GetDefaultEndpointPolicy()
    {
        if (!IsEndpointPolicyConfigurationValid)
        {
            LogEndpointPolicyConfigurationIsInValid();
            return GetDenyAllEndpointPolicy();
        }

        if (DefaultEndpointPolicy is null)
        {
            DefaultEndpointPolicy = LoadDefaultEndpointPolicy();
            LoadEndpointPolicyDictionary(DefaultEndpointPolicy);
        }

        return DefaultEndpointPolicy;
    }
    public EndpointPolicy GetEndpointPolicy(string endpointName)
    {
        if (!IsEndpointPolicyConfigurationValid)
        {
            LogEndpointPolicyConfigurationIsInValid();
            return GetDenyAllEndpointPolicy();
        }

        if (DefaultEndpointPolicy is null)
        {
            DefaultEndpointPolicy = LoadDefaultEndpointPolicy();
            LoadEndpointPolicyDictionary(DefaultEndpointPolicy);
        }

        return EndpointPolicyDictionary.GetValueOrDefault(endpointName, DefaultEndpointPolicy);
    }

    public bool? ValidateConfiguration(CancellationToken cancellationToken)
    {
        IsEndpointPolicyConfigurationValid = true;
        ValidateDefaultPolicy();
        int resourceEndpointPolicyCounter = 0;
        foreach (ResourceEndpointPolicyMap resourceEndpointPolicy in resourceEndpointPolicySettings.Value.Enforce)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                IsEndpointPolicyConfigurationValid = false;
                return null;
            }
            
            resourceEndpointPolicyCounter++;
            ValidateEndpointNames(resourceEndpointPolicy, resourceEndpointPolicyCounter);
            ValidatePolicies(resourceEndpointPolicy, resourceEndpointPolicyCounter);
        }

        if (!IsEndpointPolicyConfigurationValid)
        {
            LogEndpointPolicyConfigurationIsInValid();
        }
        
        return IsEndpointPolicyConfigurationValid;
    }
    
    public void PrimeEndpointPolicies()
    {
        if (!IsEndpointPolicyConfigurationValid)
        {
            LogEndpointPolicyConfigurationIsInValid();
            return;
        }
        
        DefaultEndpointPolicy = LoadDefaultEndpointPolicy();
        LoadEndpointPolicyDictionary(DefaultEndpointPolicy);
    }

    private void LogEndpointPolicyConfigurationIsInValid()
    {
        logger.LogCritical("The appsettings.json {Section1} configuration was found to be invalid. " +
                           "A system default {DenyAll} policy has been loaded by the system which will prevent access to all endpoints. " +
                           "Please review the server's startup logs for the specific configuration issues",
            ResourceEndpointPolicySettings.SectionName,
            "DenyAll");
    }

    private EndpointPolicy LoadDefaultEndpointPolicy()
    {
        ResourceEndpointPolicy? defaultPolicy =
            resourceEndpointPolicySettings.Value.Policies.FirstOrDefault(x =>
                x.PolicyCode.ToLower().Equals(resourceEndpointPolicySettings.Value.DefaultPolicy.ToLower()));

        ArgumentNullException.ThrowIfNull(defaultPolicy);

        return new EndpointPolicy(
            AllowCreate: defaultPolicy.AllowCreate,
            AllowRead: defaultPolicy.AllowRead,
            AllowUpdate: defaultPolicy.AllowUpdate,
            AllowDelete: defaultPolicy.AllowDelete,
            AllowSearch: defaultPolicy.AllowSearch,
            AllowVersionRead: defaultPolicy.AllowVersionRead,
            AllowHistory: defaultPolicy.AllowHistory,
            AllowConditionalCreate: defaultPolicy.AllowConditionalCreate,
            AllowConditionalUpdate: defaultPolicy.AllowConditionalUpdate,
            AllowConditionalDelete: defaultPolicy.AllowConditionalDelete,
            AllowBaseTransaction: defaultPolicy.AllowBaseTransaction,
            AllowBaseBatch: defaultPolicy.AllowBaseBatch,
            AllowBaseMetadata: defaultPolicy.AllowBaseMetadata,
            AllowBaseHistory: defaultPolicy.AllowBaseHistory);
    }

    private void LoadEndpointPolicyDictionary(EndpointPolicy defaultEndpointPolicy)
    {
        foreach (FhirResourceTypeId resourceType in Enum.GetValues(typeof(FhirResourceTypeId)))
        {
            string resourceName = resourceType.GetCode();
            IEnumerable<ResourceEndpointPolicyMap> resourceTypeEndpointPolicyMap = resourceEndpointPolicySettings.Value.Enforce.Where(x
                => x.UponEndpoints.Contains(resourceName));

            if (resourceTypeEndpointPolicyMap.Any())
            {
                IEnumerable<ResourceEndpointPolicy> enforceableResourceTypeEndpointPolicyList = resourceEndpointPolicySettings.Value.Policies.Where(x
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
                    allowConditionalCreate = OnlySetIfFalse(enforceableEndpointPolicy.AllowConditionalCreate, allowConditionalCreate);
                    allowConditionalUpdate = OnlySetIfFalse(enforceableEndpointPolicy.AllowConditionalUpdate, allowConditionalUpdate);
                    allowConditionalDelete = OnlySetIfFalse(enforceableEndpointPolicy.AllowConditionalDelete, allowConditionalDelete);
                    allowBaseTransaction = OnlySetIfFalse(enforceableEndpointPolicy.AllowBaseTransaction, allowBaseTransaction);
                    allowBaseBatch = OnlySetIfFalse(enforceableEndpointPolicy.AllowBaseTransaction, allowBaseBatch);
                    allowBaseMetadata = OnlySetIfFalse(enforceableEndpointPolicy.AllowBaseTransaction, allowBaseMetadata);
                    allowBaseHistory = OnlySetIfFalse(enforceableEndpointPolicy.AllowBaseHistory, allowBaseHistory);
                    
                }

                EndpointPolicyDictionary.Add(resourceName,
                    new EndpointPolicy(
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
    }

    private static bool OnlySetIfFalse(bool enforceableValue,
        bool exisingValue)
    {
        if (!enforceableValue)
        {
            return false;
        }

        return exisingValue;
    }

    private void ValidatePolicies(ResourceEndpointPolicyMap resourceEndpointPolicyMap,
        int resourceEndpointPolicyCounter)
    {
        foreach (var policyCode in resourceEndpointPolicyMap.Policies)
        {
            var matchedPolicies = resourceEndpointPolicySettings.Value.Policies.Where(x => x.PolicyCode.ToLower().Equals(policyCode.ToLower()));
            if (!matchedPolicies.Any())
            {
                logger.LogCritical("The appsettings.json configured {Section1}.{Enforce}[{Index}].{Policies1} " +
                                   "list contains a invalid PolicyCode of {PolicyCode}. This PolicyCode could not be found in the {Section2}.{Policies2} list. ",
                    ResourceEndpointPolicySettings.SectionName,
                    nameof(resourceEndpointPolicySettings.Value.Enforce),
                    resourceEndpointPolicyCounter,
                    nameof(resourceEndpointPolicySettings.Value.Policies),
                    policyCode,
                    ResourceEndpointPolicySettings.SectionName,
                    nameof(resourceEndpointPolicySettings.Value.Policies));
                IsEndpointPolicyConfigurationValid = false;
            }

            if (matchedPolicies.Count() > 1)
            {
                logger.LogCritical("The appsettings.json configured {Section1}.{Policies} list " +
                                   "contains duplicate PolicyCodes with the code {DuplicatePolicyCode}. " +
                                   "All PolicyCodes must be unique and are case insensitive. " +
                                   "Only the first PolicyCode in the list will be applied",
                    ResourceEndpointPolicySettings.SectionName,
                    nameof(resourceEndpointPolicySettings.Value.Policies),
                    policyCode.ToLower());
                IsEndpointPolicyConfigurationValid = false;
            }
        }
    }

    private void ValidateEndpointNames(ResourceEndpointPolicyMap resourceEndpointPolicyMap,
        int resourceEndpointPolicyCounter)
    {
        foreach (var resourceName in resourceEndpointPolicyMap.UponEndpoints)
        {
            if (!fhirResourceNameSupport.IsResourceTypeString(resourceName))
            {
                logger.LogCritical("The appsettings.json configured {Section}.{Property}[{Index}].{UponEndpoints} " +
                                   "list contains a invalid endpoint name of {ResourceName}. Ensure you have the spelling and casing correct",
                    ResourceEndpointPolicySettings.SectionName,
                    nameof(resourceEndpointPolicySettings.Value.Enforce),
                    resourceEndpointPolicyCounter,
                    "UponEndpoints",
                    resourceName);
                IsEndpointPolicyConfigurationValid = false;
            }
        }
    }

    private void ValidateDefaultPolicy()
    {
        if (!resourceEndpointPolicySettings.Value.Policies.Any(x => x.PolicyCode.ToLower().Equals(resourceEndpointPolicySettings.Value.DefaultPolicy.ToLower())))
        {
            logger.LogCritical("The appsettings.json configured {Section}.{Property} " +
                               "PolicyCode can not be found in the list of {Policies)}. " +
                               "The system's default policy which blocks all access to all endpoints has been applied",
                ResourceEndpointPolicySettings.SectionName,
                nameof(resourceEndpointPolicySettings.Value.DefaultPolicy),
                nameof(resourceEndpointPolicySettings.Value.Policies));

            IsEndpointPolicyConfigurationValid = false;
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