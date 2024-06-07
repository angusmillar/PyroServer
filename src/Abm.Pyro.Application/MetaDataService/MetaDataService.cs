using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;
using SearchParamType = Abm.Pyro.Domain.Enums.SearchParamType;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.MetaDataService;

public class MetaDataService(
    ILogger<MetaDataService> logger,
    IOptions<ImplementationSettings> implementationSettings,
    IOptions<ServiceBaseUrlSettings> serviceBaseUrlSettings,
    ISearchParameterMetaDataGetByBaseResourceType searchParameterQuery) : IMetaDataService
{
    private readonly string SoftwareCode = "PyroServer";
    private readonly string SoftwareName = "Pyro Server";
    private readonly string SoftwarePublisher = "Pyro Health";
    private readonly string SoftwareVersion = "1.0";
    private readonly int SoftwareReleaseYear = 2024;
    private readonly int SoftwareReleaseMonth = 03;
    private readonly int SoftwareReleaseDay = 01;

    private IEnumerable<SearchParameterMetaDataProjection>? CommonSearchParameterList;
    private readonly Dictionary<string, List<string>> RevIncludeDictionary = new (); //key: TargetResourceName, value: List of Revinclude Strings> 

    public async Task<CapabilityStatement> GetCapabilityStatement()
    {
        await LoadCommonSearchParameterList();

        var capStat = new CapabilityStatement();
        capStat.Id = GuidSupport.NewFhirGuid();
        capStat.Version = SoftwareVersion;
        capStat.Name = implementationSettings.Value.Name;
        capStat.Title = implementationSettings.Value.Title;
        capStat.Status = PublicationStatus.Active;
        capStat.Experimental = false;
        capStat.DateElement = new FhirDateTime(year: SoftwareReleaseYear, month: SoftwareReleaseMonth, day: SoftwareReleaseDay);
        capStat.Publisher = SoftwarePublisher;
        capStat.Contact = GetContact();
        capStat.Description = $"{SoftwareName}'s capability statement";
        capStat.UseContext = null;
        capStat.Jurisdiction = null;
        capStat.Kind = CapabilityStatementKind.Capability;
        capStat.Software = GetSoftware();
        capStat.Implementation = GetImplementationComponent();
        capStat.FhirVersionElement = new Code<FHIRVersion>() { Value = FHIRVersion.N4_0_1 };
        capStat.Format = new[] { "application/fhir+json" };
        capStat.Rest = await GetRest();
        
        return capStat;
    }

    private async Task LoadCommonSearchParameterList()
    {
        IEnumerable<SearchParameterMetaDataProjection> resourceSearchParameterList = await searchParameterQuery.Get(FhirResourceTypeId.Resource);
        IEnumerable<SearchParameterMetaDataProjection> domainResourceSearchParameterList = await searchParameterQuery.Get(FhirResourceTypeId.DomainResource);
        CommonSearchParameterList = resourceSearchParameterList.Concat(domainResourceSearchParameterList);
    }

    private async Task<List<CapabilityStatement.RestComponent>> GetRest()
    {
        var restList = new List<CapabilityStatement.RestComponent>();
        var rest = new CapabilityStatement.RestComponent();
        rest.Mode = CapabilityStatement.RestfulCapabilityMode.Server;
        rest.Documentation = "General purpose FHIR server";
        rest.Security = null;
        rest.Resource = await GetRestResourceList();
        rest.Interaction = GetRestInteraction();
        
        restList.Add(rest);
        
        return restList;
    }

    private async Task<List<CapabilityStatement.ResourceComponent>> GetRestResourceList()
    {
        FhirResourceTypeId[] abstractResourceTypeList = [FhirResourceTypeId.Resource, FhirResourceTypeId.DomainResource];
        var resourceComponentList = new List<CapabilityStatement.ResourceComponent>();
        foreach (FhirResourceTypeId resourceTypeId in Enum.GetValues(typeof(FhirResourceTypeId)).Cast<FhirResourceTypeId>())
        {
            if (!abstractResourceTypeList.Contains(resourceTypeId))
            {
                CapabilityStatement.ResourceComponent? restResource = await GetRestResource(resourceTypeId);
                if (restResource is not null)
                {
                    resourceComponentList.Add(restResource);
                }                
            }
        }

        //Populate the revincludes now that we have iterated all resources search parameters 
        foreach (var restResource in resourceComponentList)
        {
            restResource.SearchRevInclude = GetResourceSearchRevInclude(restResource.Type);    
        }
        
        return resourceComponentList;
    }

    private async Task<CapabilityStatement.ResourceComponent?> GetRestResource(FhirResourceTypeId resourceTypeId)
    {
        var restResource = new CapabilityStatement.ResourceComponent();
        restResource.Type = resourceTypeId.GetCode();
        restResource.Profile = Path.Combine("http://hl7.org/fhir/StructureDefinition", resourceTypeId.GetCode());
        restResource.Documentation = resourceTypeId.GetCode();
        restResource.Interaction = GetRestResourceInteraction();
        restResource.Versioning = CapabilityStatement.ResourceVersionPolicy.Versioned;
        restResource.ReadHistory = true;
        restResource.UpdateCreate = true;
        restResource.ConditionalCreate = true;
        restResource.ConditionalRead = CapabilityStatement.ConditionalReadStatus.FullSupport;
        restResource.ConditionalUpdate = true;
        restResource.ConditionalDelete = CapabilityStatement.ConditionalDeleteStatus.Single;
        restResource.ReferencePolicyElement = GetRestResourceReferencePolicy();
        
        ArgumentNullException.ThrowIfNull(CommonSearchParameterList);
        IEnumerable<SearchParameterMetaDataProjection> searchParameterList = CommonSearchParameterList.Concat(await searchParameterQuery.Get(resourceTypeId));
        
        restResource.SearchInclude = GetResourceSearchInclude(resourceTypeId, searchParameterList);
        restResource.SearchParam = GetResourceSearchParam(searchParameterList);
        restResource.Operation = GetResourceOperation(resourceTypeId);
        
        return restResource;
    }

    private static List<CapabilityStatement.SearchParamComponent> GetResourceSearchParam(IEnumerable<SearchParameterMetaDataProjection> searchParameterList)
    {
        var searchParamComponentList = new List<CapabilityStatement.SearchParamComponent>();
        foreach (var searchParameter in searchParameterList)
        {
            var searchParamComponent = new CapabilityStatement.SearchParamComponent();
            searchParamComponent.Name = searchParameter.Code;
            searchParamComponent.Definition = searchParameter.Url.OriginalString;
            searchParamComponent.Type = MapSearchParamType(searchParameter.Type);
            
            searchParamComponentList.Add(searchParamComponent);
        }

        return searchParamComponentList;
        
    }

    private static List<Code<CapabilityStatement.ReferenceHandlingPolicy>> GetRestResourceReferencePolicy()
    {
        return new List<Code<CapabilityStatement.ReferenceHandlingPolicy>>()
        {
            new Code<CapabilityStatement.ReferenceHandlingPolicy>()
            {
                Value = CapabilityStatement.ReferenceHandlingPolicy.Literal
            },
            new Code<CapabilityStatement.ReferenceHandlingPolicy>()
            {
                Value = CapabilityStatement.ReferenceHandlingPolicy.Local
            }
        };
    }

    private static List<CapabilityStatement.OperationComponent> GetResourceOperation(FhirResourceTypeId resourceTypeId)
    {
        //Currently there are none
        return Enumerable.Empty<CapabilityStatement.OperationComponent>().ToList();
    }

    private IEnumerable<string> GetResourceSearchInclude(FhirResourceTypeId resourceTypeId, IEnumerable<SearchParameterMetaDataProjection> searchParameterList)
    {
        var includeList = new List<string>();
        var referenceSearchParameterList 
            = searchParameterList.Where(x =>
            x.Type == SearchParamType.Reference);

        foreach (var referenceSearchParameter in referenceSearchParameterList)
        {
            if (!referenceSearchParameter.TargetList.Any())
            {
                logger.LogWarning("When constructing the server's CapabilityStatement at the [base]/metadata endpoint a the search parameter " +
                                  "{SearchParameterName} of type Reference for the Resource type {ResourceType} was found to have zero reference targets",
                    referenceSearchParameter.Code,
                    resourceTypeId.GetCode());
                continue;
            }
            
            includeList.Add($"{resourceTypeId.GetCode()}:{referenceSearchParameter.Code}");
            
            //Collect revIncludes for later inclusion in their target resources revinclude list  
            CollectRevIncludes(resourceTypeId, referenceSearchParameter);
        }

        return includeList;
        
    }

    private void CollectRevIncludes(FhirResourceTypeId resourceTypeId,
        SearchParameterMetaDataProjection referenceSearchParameter)
    {
        foreach (var target in referenceSearchParameter.TargetList)
        {
            if (RevIncludeDictionary.TryGetValue(target.ResourceType.GetCode(), out List<string>? value))
            {
                value.Add($"{resourceTypeId.GetCode()}:{referenceSearchParameter.Code}");
            }
            else
            {
                RevIncludeDictionary.Add(target.ResourceType.GetCode(), new List<string>() { $"{resourceTypeId.GetCode()}:{referenceSearchParameter.Code}"});
            }
        }
    }

    private IEnumerable<string> GetResourceSearchRevInclude(string resourceName)
    {
        if (RevIncludeDictionary.TryGetValue(resourceName, out List<string>? revIncludeList))
        {
            return revIncludeList;
        }
        return Enumerable.Empty<string>().ToList();
    }

    private static List<CapabilityStatement.ResourceInteractionComponent> GetRestResourceInteraction()
    {
        return new List<CapabilityStatement.ResourceInteractionComponent>()
        {
            new CapabilityStatement.ResourceInteractionComponent()
            {
                Code = CapabilityStatement.TypeRestfulInteraction.Read
            },
            new CapabilityStatement.ResourceInteractionComponent()
            {
                Code = CapabilityStatement.TypeRestfulInteraction.Vread
            },
            new CapabilityStatement.ResourceInteractionComponent()
            {
                Code = CapabilityStatement.TypeRestfulInteraction.Update
            },
            new CapabilityStatement.ResourceInteractionComponent()
            {
                Code = CapabilityStatement.TypeRestfulInteraction.Delete
            },
            new CapabilityStatement.ResourceInteractionComponent()
            {
                Code = CapabilityStatement.TypeRestfulInteraction.HistoryInstance
            },
            new CapabilityStatement.ResourceInteractionComponent()
            {
                Code = CapabilityStatement.TypeRestfulInteraction.HistoryType
            },
            new CapabilityStatement.ResourceInteractionComponent()
            {
                Code = CapabilityStatement.TypeRestfulInteraction.Create
            }
        };
    }

    private static List<CapabilityStatement.SystemInteractionComponent> GetRestInteraction()
    {
        return new List<CapabilityStatement.SystemInteractionComponent>()
        {
            new CapabilityStatement.SystemInteractionComponent()
            {
                Code = CapabilityStatement.SystemRestfulInteraction.Transaction
            },
            new CapabilityStatement.SystemInteractionComponent()
            {
                Code = CapabilityStatement.SystemRestfulInteraction.HistorySystem
            }
        };
    }

    private CapabilityStatement.ImplementationComponent GetImplementationComponent()
    {
        return new CapabilityStatement.ImplementationComponent()
        {
            Description = implementationSettings.Value.Description,
            Url = Path.Combine(serviceBaseUrlSettings.Value.Url.OriginalString, "fhir")
        };
    }

    private CapabilityStatement.SoftwareComponent GetSoftware()
    {
        return new CapabilityStatement.SoftwareComponent()
        {
            Name = SoftwareCode,
            Version = SoftwareVersion,
            ReleaseDateElement = new FhirDateTime(year: SoftwareReleaseYear, month: SoftwareReleaseMonth, day: SoftwareReleaseDay)
        };
    }

    private static List<ContactDetail> GetContact()
    {
        return new List<ContactDetail>()
        {
            new ContactDetail()
            {
                Name = "Angus Millar",
                Telecom = new List<ContactPoint>()
                {
                    new ContactPoint(system: ContactPoint.ContactPointSystem.Email, use: ContactPoint.ContactPointUse.Work, value: "angusbmillar@gmail.com")
                }
            }
        };
    }

    private static Hl7.Fhir.Model.SearchParamType? MapSearchParamType(Domain.Enums.SearchParamType searchParamType)
    {
        switch (searchParamType)
        {
            case SearchParamType.Number:
                return Hl7.Fhir.Model.SearchParamType.Number;
            case SearchParamType.Date:
                return Hl7.Fhir.Model.SearchParamType.Date;
            case SearchParamType.String:
                return Hl7.Fhir.Model.SearchParamType.String;
            case SearchParamType.Token:
                return Hl7.Fhir.Model.SearchParamType.Token;
            case SearchParamType.Reference:
                return Hl7.Fhir.Model.SearchParamType.Reference;
            case SearchParamType.Composite:
                return Hl7.Fhir.Model.SearchParamType.Composite;
            case SearchParamType.Quantity:
                return Hl7.Fhir.Model.SearchParamType.Quantity;
            case SearchParamType.Uri:
                return Hl7.Fhir.Model.SearchParamType.Uri;
            case SearchParamType.Special:
                return Hl7.Fhir.Model.SearchParamType.Special;
            default:
                throw new ArgumentOutOfRangeException(nameof(searchParamType), searchParamType, null);
        }
    }
}