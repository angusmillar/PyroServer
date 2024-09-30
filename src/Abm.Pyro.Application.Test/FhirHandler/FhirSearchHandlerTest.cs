using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Microsoft.Extensions.Primitives;
using Moq;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirQuery;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;
using Xunit;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.Test.FhirHandler;

public class FhirSearchHandlerTest
{
    private Mock<IValidator> ValidatorMock;
    private readonly IFhirResourceTypeSupport FhirResourceTypeSupport;
    private readonly ISearchQueryService SearchQueryService;
    private readonly IResourceStoreSearch ResourceStoreSearch;
    private readonly IFhirBundleCreationSupport FhirBundleCreationSupport;
    private readonly IPaginationSupport PaginationSupport;
    private readonly IFhirResponseHttpHeaderSupport FhirResponseHttpHeaderSupport;
    //Setup
    protected FhirSearchHandlerTest()
    {
        var patientResourceOne = GetPatientResourceOne();
        var patientResourceTwo= GetPatientResourceTwo();
        var resourceStoreList = GetResourceStoreList(new []{ patientResourceOne, patientResourceTwo});
        
        ValidatorMock = new Mock<IValidator>();
        ValidatorMock.Setup(x => 
                x.Validate(
                    It.IsAny<IValidatable>()))
            .Returns(new ValidatorResult(isValid: true, httpStatusCode: null, operationOutcome: null));
        
        FhirResourceTypeSupport = new FhirResourceTypeSupport();

        var searchQueryServiceOutcome = new SearchQueryServiceOutcome(
            resourceContext: FhirResourceTypeId.Patient,
            fhirQuery: new FhirQuery()
            );
        
        var searchQueryServiceMock = new Mock<ISearchQueryService>();
        searchQueryServiceMock.Setup(x =>
                x.Process(It.IsAny<FhirResourceTypeId>(), It.IsAny<string?>()))
            .Returns(Task.FromResult((SearchQueryServiceOutcome)searchQueryServiceOutcome));
        SearchQueryService = searchQueryServiceMock.Object;

        var resourceStoreSearchOutcome = new ResourceStoreSearchOutcome(
            searchTotal: 2, 
            pageRequested: 1, 
            pagesTotal: 1, 
            resourceStoreList: resourceStoreList,
            includedResourceStoreList: new List<ResourceStore>());
        
        var resourceStoreSearchMock = new Mock<IResourceStoreSearch>();
        resourceStoreSearchMock.Setup(x => 
            x.GetSearch(It.IsAny<SearchQueryServiceOutcome>()))
            .Returns(Task.FromResult(resourceStoreSearchOutcome));
        ResourceStoreSearch = resourceStoreSearchMock.Object;
        
        var bundle = new Bundle()
        {
            Id = GuidSupport.NewFhirGuid(),
            Total = resourceStoreList.Count,
            Entry = new List<Bundle.EntryComponent>()
            {
                new Bundle.EntryComponent()
                {
                    Resource = patientResourceOne,
                    FullUrl = $"{patientResourceOne.TypeName}/{patientResourceOne.Id}"
                },
                new Bundle.EntryComponent()
                {
                    Resource = patientResourceTwo,
                    FullUrl = $"{patientResourceTwo.TypeName}/{patientResourceTwo.Id}"
                }
            }
        };
        
        var fhirBundleCreationSupportMock = new Mock<IFhirBundleCreationSupport>();
        fhirBundleCreationSupportMock.Setup(x => 
                x.CreateBundle(
                    It.IsAny<ResourceStoreSearchOutcome>(), 
                    It.IsAny<Bundle.BundleType>(), 
                    It.IsAny<string>()))
            .Returns(Task.FromResult(bundle));
        FhirBundleCreationSupport = fhirBundleCreationSupportMock.Object;

        var paginationSupportMock = new Mock<IPaginationSupport>();
        paginationSupportMock.Setup(x =>
            x.SetBundlePagination(
                It.IsAny<Bundle>(),
                It.IsAny<SearchQueryServiceOutcome>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>()));    
        PaginationSupport = paginationSupportMock.Object;
        
        FhirResponseHttpHeaderSupport = new FhirResponseHttpHeaderSupport();
        
    }

    private static List<ResourceStore> GetResourceStoreList(IEnumerable<Resource> resourceList)
    {
        var result = new List<ResourceStore>();

        int resourceStoreIdCounter = 1;
        foreach (var resource in resourceList)
        {
            var resourceStoreOne = new ResourceStore(
                resourceStoreId: resourceStoreIdCounter,
                resourceId: resource.Id,
                versionId: 10,
                isCurrent: true,
                isDeleted: false,
                resourceType: FhirResourceTypeId.Patient,
                httpVerb: HttpVerbId.Put,
                json: resource.ToJson(),
                lastUpdatedUtc: new DateTime(2023, 01, 01, 10, 00, 00), //utc
                indexReferenceList: new List<IndexReference>(),
                indexStringList: new List<IndexString>(),
                indexDateTimeList: new List<IndexDateTime>(),
                indexQuantityList: new List<IndexQuantity>(),
                indexTokenList: new List<IndexToken>(),
                indexUriList: new List<IndexUri>(),
                rowVersion: 100);
            
            result.Add(resourceStoreOne);
            
            resourceStoreIdCounter++;
        }
        
        return result;
    }

    private static Patient GetPatientResourceOne()
    {
        var patientResource = new Patient()
        {
            Id = "test-patient-one-id",
            Name = new List<HumanName>()
            {
                new HumanName()
                {
                    Family = "duck",
                    Given = new[] { "donald" }
                }
            },
            Gender = AdministrativeGender.Male,
            BirthDate = "1973-09-30"
        };
        return patientResource;
    }
    
    private static Patient GetPatientResourceTwo()
    {
        var patientResource = new Patient()
        {
            Id = "test-patient-two--id",
            Name = new List<HumanName>()
            {
                new HumanName()
                {
                    Family = "mouse",
                    Given = new[] { "Minnie" }
                }
            },
            Gender = AdministrativeGender.Female,
            BirthDate = "1990-10-29"
        };
        return patientResource;
    }


    public class Handle : FhirSearchHandlerTest
    {
        [Fact]
        public async Task StandardSearch_IsOk()
        {
            //Arrange
            var target = new FhirSearchHandler(
                ValidatorMock.Object,
                FhirResourceTypeSupport,
                SearchQueryService,
                ResourceStoreSearch,
                FhirBundleCreationSupport,
                PaginationSupport,
                FhirResponseHttpHeaderSupport);
                
            var cancellationTokenSource = new CancellationTokenSource();

            var timeStamp = DateTimeOffset.Now;
            var fhirReadRequest = new FhirSearchRequest(
                RequestSchema: "http",
                tenant: "test-tenant",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>(),
                ResourceName: ResourceType.Patient.GetLiteral(),
                TimeStamp: timeStamp);
            
            //Act
            FhirResourceResponse response = await target.Handle(request: fhirReadRequest, cancellationToken: cancellationTokenSource.Token);

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
            Assert.NotNull(response.Resource);
            Assert.IsType<Bundle>(response.Resource);
            Bundle? bundle = response.Resource as Bundle;
            Assert.Equal(2, bundle!.Total);
            Assert.NotNull(response.Headers);
            Assert.Equal(timeStamp.ToString("r"), response.Headers[HttpHeaderName.Date]);
        }
        
        [Fact]
        public async Task InvalidSearchParameter_IsBadRequest()
        {
            //Arrange
            ValidatorMock = new Mock<IValidator>();
            
            ValidatorMock.Setup(x => 
                    x.Validate(
                        It.IsAny<FhirSearchRequest>()))
                .Returns(new ValidatorResult(isValid: true, httpStatusCode: null, operationOutcome: null));
            
            ValidatorMock.Setup(x => 
                    x.Validate(
                        It.IsAny<SearchQueryServiceOutcomeAndHeaders>()))
                .Returns(new ValidatorResult(
                    isValid: false, 
                    httpStatusCode: HttpStatusCode.BadRequest,
                    operationOutcome: new OperationOutcome() {Id = "operationOutcome-resource-id"}));
            
            var target = new FhirSearchHandler(
                ValidatorMock.Object,
                FhirResourceTypeSupport,
                SearchQueryService,
                ResourceStoreSearch,
                FhirBundleCreationSupport,
                PaginationSupport,
                FhirResponseHttpHeaderSupport);
                
            var cancellationTokenSource = new CancellationTokenSource();

            var timeStamp = DateTimeOffset.Now;
            var fhirReadRequest = new FhirSearchRequest(
                RequestSchema: "http",
                tenant: "test-tenant",
                RequestPath: "fhir",
                QueryString: "NotASearchParameter=rubbish", //The invalid search parameter
                Headers: new Dictionary<string, StringValues>()
                {
                    {HttpHeaderName.Prefer, new StringValues($"Handling={PreferHandlingType.Strict}") }
                },
                ResourceName: ResourceType.Patient.GetLiteral(),
                TimeStamp: timeStamp);
            
            //Act
            FhirResourceResponse response = await target.Handle(request: fhirReadRequest, cancellationToken: cancellationTokenSource.Token);

            //Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.HttpStatusCode);
            Assert.NotNull(response.Resource);
            Assert.IsType<OperationOutcome>(response.Resource);
            Assert.NotNull(response.Headers);
        }
    }
}