using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Primitives;
using Moq;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.Indexing;
using Abm.Pyro.Application.Notification;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Validation;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.Test.FhirHandler;

public class FhirCreateHandlerTest
{
    private readonly Mock<IValidator> _validatorMock;
    private readonly Mock<IResourceStoreAdd> _resourceStoreAddMock;
    private readonly Mock<IFhirSerializationSupport> _fhirSerializationSupportMock;
    private readonly IFhirResourceTypeSupport _fhirResourceTypeSupport;
    private readonly IFhirResponseHttpHeaderSupport _fhirResponseHttpHeaderSupport;
    private readonly Mock<IIndexer> _indexerMock;
    private readonly Mock<IPreferredReturnTypeService> _preferredReturnTypeServiceMock;
    private readonly Mock<IServiceBaseUrlCache> _serviceBaseUrlCacheMock;
    private readonly Mock<IRepositoryEventCollector> _repositoryEventCollectorMock;

    //Setup
    protected FhirCreateHandlerTest()
    {
        var now = DateTime.Now;
        
        Observation observationResourceFromDbAdd = GetObservationResource();
        
        ResourceStore? resourceStoreFromDbAdd = new ResourceStore(
            resourceStoreId: 1,
            resourceId: observationResourceFromDbAdd.Id,
            versionId: int.Parse(observationResourceFromDbAdd.VersionId),
            isCurrent: true,
            isDeleted: false,
            resourceType: FhirResourceTypeId.Observation,
            httpVerb: HttpVerbId.Post,
            json: observationResourceFromDbAdd.ToJson(),
            lastUpdatedUtc: new DateTime(2023, 01, 01, 10, 00, 00), //utc
            indexReferenceList: new List<IndexReference>(),
            indexStringList: new List<IndexString>(),
            indexDateTimeList: new List<IndexDateTime>(),
            indexQuantityList: new List<IndexQuantity>(),
            indexTokenList: new List<IndexToken>(),
            indexUriList: new List<IndexUri>(),
            rowVersion: 100);

        _validatorMock = new Mock<IValidator>();
        _validatorMock.Setup(x => 
            x.Validate(
                It.IsAny<IValidatable>()))
            .Returns(new ValidatorResult(isValid: true, httpStatusCode: null, operationOutcome: null));
        
        _resourceStoreAddMock = new Mock<IResourceStoreAdd>();
        _resourceStoreAddMock.Setup(x =>
                x.Add(
                    It.IsAny<ResourceStore>()))
            .ReturnsAsync((ResourceStore resourceStore) => resourceStoreFromDbAdd);
        
        _fhirResourceTypeSupport = new FhirResourceTypeSupport();

        _fhirSerializationSupportMock = new Mock<IFhirSerializationSupport>();
        _fhirSerializationSupportMock.Setup(x =>
                x.ToJson(
                    It.IsAny<Resource>(),
                    It.IsAny<Hl7.Fhir.Rest.SummaryType?>(),
                    It.IsAny<bool>()))
            .Returns("The Observation resource's JSON string would be here, but why bother for the unit test!");


        _fhirResponseHttpHeaderSupport = new FhirResponseHttpHeaderSupport();

        var indexerOutcome = new IndexerOutcome(
            stringIndexList: new List<IndexString>(),
            referenceIndexList: new List<IndexReference>(),
            dateTimeIndexList: new List<IndexDateTime>(),
            quantityIndexList: new List<IndexQuantity>(),
            tokenIndexList: new List<IndexToken>(),
            uriIndexList: new List<IndexUri>());

        _indexerMock = new Mock<IIndexer>();
        _indexerMock.Setup(x =>
                x.Process(
                    It.IsAny<Resource>(),
                    It.IsAny<FhirResourceTypeId>()))
            .ReturnsAsync(indexerOutcome);

        
        _repositoryEventCollectorMock = new Mock<IRepositoryEventCollector>();
        _repositoryEventCollectorMock
            .Setup(x => 
                x.Add(It.IsAny<RepositoryEvent>()));
        
        var responseObservationResource = GetObservationResource();
        var fhirOptionalResourceResponse = new FhirOptionalResourceResponse(
            Resource: responseObservationResource,
            HttpStatusCode: HttpStatusCode.Created,
            Headers: new Dictionary<string, StringValues>(),
            RepositoryEventCollector: _repositoryEventCollectorMock.Object);

        _preferredReturnTypeServiceMock = new Mock<IPreferredReturnTypeService>();
        _preferredReturnTypeServiceMock
            .Setup(x =>
                x.GetResponse(
                    It.IsAny<HttpStatusCode>(),
                    It.IsAny<Resource>(),
                    It.IsAny<int>(),
                    It.IsAny<Dictionary<string, StringValues>>(),
                    It.IsAny<Dictionary<string, StringValues>>(),
                    It.IsAny<IRepositoryEventCollector>()))
            .Returns(fhirOptionalResourceResponse);
        
        _serviceBaseUrlCacheMock = new Mock<IServiceBaseUrlCache>();
        _serviceBaseUrlCacheMock
            .Setup(x =>
                x.GetRequiredPrimaryAsync())
            .ReturnsAsync(new ServiceBaseUrl(serviceBaseUrlId: 1, url: "https://thisFhirServer.com.au/fhir", isPrimary: true));

        

    }


    private static Observation GetObservationResource()
    {
        var observationResource = new Observation()
        {
            Id = "test-patient-one-id",
            Meta = new Meta()
            {
                VersionId = "1"
            },
            Status = ObservationStatus.Final,
            Code = new CodeableConcept("https://some-server.com.au/CodeSystem/testing", "TestCode", "Test Code Display", "Test Code Text"),
            Value = new Quantity(50, "ml")
        };
        return observationResource;
    }


    public class Handle : FhirCreateHandlerTest
    {
        [Fact]
        public async Task create_IsOk()
        {
            //Arrange
            var target = new FhirCreateHandler(
                _validatorMock.Object,
                _resourceStoreAddMock.Object,
                _fhirSerializationSupportMock.Object,
                _fhirResourceTypeSupport,
                _fhirResponseHttpHeaderSupport,
                _indexerMock.Object,
                _preferredReturnTypeServiceMock.Object,
                _serviceBaseUrlCacheMock.Object,
                _repositoryEventCollectorMock.Object
            );

            var cancellationTokenSource = new CancellationTokenSource();

            Observation observationResourceToPost = GetObservationResource();
            
            var timeStamp = DateTimeOffset.Now;
            var fhirUpdateRequest = new FhirCreateRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: GuidSupport.NewFhirGuid(),
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>(),
                ResourceName: observationResourceToPost.TypeName,
                Resource: observationResourceToPost,
                ResourceId: null,
                TimeStamp: timeStamp);

            //Act
            FhirOptionalResourceResponse response = await target.Handle(request: fhirUpdateRequest, cancellationToken: cancellationTokenSource.Token);

            //Verify
            _resourceStoreAddMock.Verify(x => 
                    x.Add(It.Is<ResourceStore>(r => 
                        r.VersionId.Equals(1) & 
                        !string.IsNullOrWhiteSpace(r.ResourceId) &
                        r.LastUpdatedUtc.Equals(timeStamp.UtcDateTime) &
                                                r.IsCurrent.Equals(true) &
                                                r.IsDeleted.Equals(false) &
                        r.ResourceType.Equals(FhirResourceTypeId.Observation) &
                        !string.IsNullOrWhiteSpace(r.Json))
                        ),
                times: Times.Once);
            
            _preferredReturnTypeServiceMock.Verify(x =>
                x.GetResponse(
                    It.Is<HttpStatusCode>(s => s.Equals(HttpStatusCode.Created)),
                    It.IsAny<Resource>(),
                    It.IsAny<int>(),
                    It.IsAny<Dictionary<string, StringValues>>(),
                    It.IsAny<Dictionary<string, StringValues>>(),
                    It.IsAny<IRepositoryEventCollector>()),
                times: Times.Once);

            //Assert
            Assert.Equal(HttpStatusCode.Created, response.HttpStatusCode);
            Assert.NotNull(response.Resource);
            Assert.IsType<Observation>(response.Resource);
            Observation? observation = response.Resource as Observation;
            Assert.Equal("1", observation!.VersionId);
            Assert.NotNull(response.Headers);

        }
    }
}