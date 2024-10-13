using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Abm.Pyro.Application.Cache;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.Indexing;
using Abm.Pyro.Application.Notification;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Tenant;
using Abm.Pyro.Application.Test.Factories;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirQuery;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;
using Xunit;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.Test.FhirHandler;

public class FhirUpdateHandlerTest
{
    private readonly Mock<IValidator> _validatorMock;
    private readonly IFhirResourceTypeSupport _fhirResourceTypeSupport;
    private readonly Mock<IResourceStoreGetForUpdateByResourceId> _resourceStoreGetForUpdateByResourceIdMock;
    private readonly Mock<IRequestHandler<FhirCreateRequest, FhirOptionalResourceResponse>> _fhirCreateHandlerMock;
    private readonly Mock<IResourceStoreAdd> _resourceStoreAddMock;
    private readonly IIndexer _indexer;
    private readonly IFhirSerializationSupport _fhirSerializationSupport;
    private readonly IResourceStoreUpdate _resourceStoreUpdate;
    private readonly IFhirResponseHttpHeaderSupport _fhirResponseHttpHeaderSupport;
    private readonly IFhirRequestHttpHeaderSupport _fhirRequestHttpHeaderSupport;
    private readonly IOperationOutcomeSupport _operationOutcomeSupport;
    private readonly Mock<IPreferredReturnTypeService> _preferredReturnTypeServiceMock;
    private readonly Mock<IOptions<IndexingSettings>> _indexingSettingsOptionsMock;
    private readonly Mock<IRepositoryEventCollector> _repositoryEventCollectorMock;
    private readonly DateTime _now;
   
    //Setup
    protected FhirUpdateHandlerTest()
    {
        
        _now = DateTime.Now;
        
        _validatorMock = new Mock<IValidator>();
        _validatorMock.Setup(x => 
                x.Validate(
                    It.IsAny<IValidatable>()))
            .Returns(new ValidatorResult(isValid: true, httpStatusCode: null, operationOutcome: null));
        
        _fhirResourceTypeSupport = new FhirResourceTypeSupport();

        var observationResource = GetObservationResource();
        
        ResourceStoreUpdateProjection? resourceStoreFoundForUpdate = new ResourceStoreUpdateProjection(
            resourceStoreId: 1,
            versionId: int.Parse(observationResource.VersionId),
            isCurrent: true,
            isDeleted: false);
        
        _resourceStoreGetForUpdateByResourceIdMock = new Mock<IResourceStoreGetForUpdateByResourceId>();
        _resourceStoreGetForUpdateByResourceIdMock.Setup(x => 
            x.Get(
                It.IsAny<FhirResourceTypeId>(), 
                It.IsAny<string>()))
            .ReturnsAsync((ResourceStoreUpdateProjection?)resourceStoreFoundForUpdate);
        
        ResourceStore? resourceStoreHistoryEntity = new ResourceStore(
            resourceStoreId: 1,
            resourceId: observationResource.Id,
            versionId: int.Parse(observationResource.VersionId + 1),
            isCurrent: false,
            isDeleted: false,
            resourceType: FhirResourceTypeId.Observation,
            httpVerb: HttpVerbId.Post,
            json: observationResource.ToJson(),
            lastUpdatedUtc: new DateTime(2023, 01, 01, 10, 00, 00), //utc
            indexReferenceList: new List<IndexReference>(),
            indexStringList: new List<IndexString>(),
            indexDateTimeList: new List<IndexDateTime>(),
            indexQuantityList: new List<IndexQuantity>(),
            indexTokenList: new List<IndexToken>(),
            indexUriList: new List<IndexUri>(),
            rowVersion: 100);
        
        _resourceStoreAddMock = new Mock<IResourceStoreAdd>();
        _resourceStoreAddMock.Setup(x => 
            x.Add(
                It.IsAny<ResourceStore>()))
            .ReturnsAsync((ResourceStore)resourceStoreHistoryEntity);

        var dateTimeProviderMock = new Mock<IDateTimeProvider>();
        dateTimeProviderMock
            .Setup(x => x.Now)
            .Returns(new DateTime(2024, 01, 01, 10, 00, 00));
        
        var repositoryEventCollector = new RepositoryEventCollector(TenantServiceFactory.GetTest(), dateTimeProviderMock.Object);
        repositoryEventCollector.Add(new RepositoryEvent(
            ResourceType: FhirResourceTypeId.Observation,
            RequestId: "requestId",
            RepositoryEventType: RepositoryEventType.Create,
            ResourceId: observationResource.Id,
            Tenant: TenantServiceFactory.GetTest().GetScopedTenant(),
            EventTimestampUtc: _now.ToUniversalTime()));
        
        var createdObservationResource = GetObservationResource();
        var fhirResponse = new FhirOptionalResourceResponse(
            Resource: observationResource,
            HttpStatusCode: HttpStatusCode.Created,
            Headers: new Dictionary<string, StringValues>()
            {
                { HttpHeaderName.Date, new StringValues(_now.ToString("r")) },
                { HttpHeaderName.LastModified, new StringValues(_now.ToString("r")) },
                { HttpHeaderName.ETag, new StringValues(createdObservationResource.VersionId) },
                {
                    HttpHeaderName.Location,
                    new StringValues(
                        $"{createdObservationResource.TypeName}/{createdObservationResource.Id}/_history/{createdObservationResource.VersionId}")
                }

            },
            RepositoryEventCollector: repositoryEventCollector);
        
        _fhirCreateHandlerMock = new Mock<IRequestHandler<FhirCreateRequest, FhirOptionalResourceResponse>>();
        _fhirCreateHandlerMock.Setup(x => 
            x.Handle(
                It.IsAny<FhirCreateRequest>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fhirResponse);
        
        var indexerOutcome = new IndexerOutcome(
            stringIndexList: new List<IndexString>(),
            referenceIndexList: new List<IndexReference>(),
            dateTimeIndexList: new List<IndexDateTime>(),
            quantityIndexList: new List<IndexQuantity>(),
            tokenIndexList: new List<IndexToken>(),
            uriIndexList: new List<IndexUri>());
        
        var indexerMock = new Mock<IIndexer>();
        indexerMock.Setup(x => 
            x.Process(
                It.IsAny<Resource>(), 
                It.IsAny<FhirResourceTypeId>()))
            .ReturnsAsync(indexerOutcome);
        _indexer = indexerMock.Object;

        var fhirSerializationSupportMock = new Mock<IFhirSerializationSupport>();
        fhirSerializationSupportMock.Setup(x => 
            x.ToJson(
                It.IsAny<Resource>(),
                It.IsAny<Hl7.Fhir.Rest.SummaryType?>(), 
                It.IsAny<bool>()))
            .Returns("The Observation resource's JSON object would be here, but why bother for the unit test!");
        _fhirSerializationSupport = fhirSerializationSupportMock.Object;


        ResourceStore? resourceStoreUpdate = new ResourceStore(
            resourceStoreId: 1,
            resourceId: observationResource.Id,
            versionId: int.Parse(observationResource.VersionId) + 1,
            isCurrent: true,
            isDeleted: false,
            resourceType: FhirResourceTypeId.Observation,
            httpVerb: HttpVerbId.Post,
            json: observationResource.ToJson(),
            lastUpdatedUtc: _now, //utc
            indexReferenceList: new List<IndexReference>(),
            indexStringList: new List<IndexString>(),
            indexDateTimeList: new List<IndexDateTime>(),
            indexQuantityList: new List<IndexQuantity>(),
            indexTokenList: new List<IndexToken>(),
            indexUriList: new List<IndexUri>(),
            rowVersion: 100);
        
        var resourceStoreUpdateMock = new Mock<IResourceStoreUpdate>();
        resourceStoreUpdateMock.Setup(x => 
            x.Update(
                It.IsAny<ResourceStoreUpdateProjection>(),
                It.IsAny<bool>()));
        _resourceStoreUpdate = resourceStoreUpdateMock.Object;
        
        _fhirResponseHttpHeaderSupport = new FhirResponseHttpHeaderSupport();
        _fhirRequestHttpHeaderSupport = new FhirRequestHttpHeaderSupport();

        var operationOutcome = new OperationOutcome();
        operationOutcome.Issue = new List<OperationOutcome.IssueComponent>()
        {
            new OperationOutcome.IssueComponent()
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.Conflict,
                Diagnostics = $"{HttpHeaderName.IfMatch} header precondition failure. Version update was for version ?? however " +
            $"the server found version ??. "
            }
        };
        var operationOutcomeSupportMock = new Mock<IOperationOutcomeSupport>();
        operationOutcomeSupportMock.Setup(x => x.GetError(It.IsAny<string[]>())).Returns(operationOutcome);
        _operationOutcomeSupport = operationOutcomeSupportMock.Object;
        
        
        var updatedObservationResource = GetObservationResource();
        updatedObservationResource.Meta = new Meta()
        {
            LastUpdated = DateTimeOffset.Now,
            VersionId = "2"
        };
        
        var fhirOptionalResourceResponse = new FhirOptionalResourceResponse(
            Resource: updatedObservationResource, 
            HttpStatusCode: HttpStatusCode.OK, 
            Headers: new Dictionary<string, StringValues>()
            {
                {HttpHeaderName.Date, new StringValues(_now.ToString("r"))},
                {HttpHeaderName.LastModified, new StringValues(_now.ToString("r"))},
                {HttpHeaderName.ETag, new StringValues(updatedObservationResource.VersionId)}
            },
            RepositoryEventCollector: repositoryEventCollector);
        
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
        
        
        _indexingSettingsOptionsMock = new Mock<IOptions<IndexingSettings>>();
        _indexingSettingsOptionsMock.Setup(x => x.Value).Returns(new IndexingSettings() { RemoveHistoricResourceIndexesOnUpdateOrDelete = true});

        _repositoryEventCollectorMock = new Mock<IRepositoryEventCollector>();
        _repositoryEventCollectorMock
            .Setup(x => 
                x.Add(It.IsAny<RepositoryEvent>()));
        
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
    

    public class Handle : FhirUpdateHandlerTest
    {
        [Fact]
        public async Task update_IsOk()
        {
            
            //Arrange
            var target = new FhirUpdateHandler(
                _validatorMock.Object,
                _fhirResourceTypeSupport,
                _resourceStoreGetForUpdateByResourceIdMock.Object,
                _fhirCreateHandlerMock.Object,
                _resourceStoreAddMock.Object,
                _indexer,
                _fhirSerializationSupport,
                _resourceStoreUpdate,
                _fhirResponseHttpHeaderSupport,
                _fhirRequestHttpHeaderSupport,
                _operationOutcomeSupport,
                _preferredReturnTypeServiceMock.Object,
                _indexingSettingsOptionsMock.Object,
                _repositoryEventCollectorMock.Object);
                
            var cancellationTokenSource = new CancellationTokenSource();

            var timeStamp = DateTimeOffset.Now;
            var fhirUpdateRequest = new FhirUpdateRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: "Request Id",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>(),
                ResourceName: ResourceType.Observation.GetLiteral(),
                Resource: new Observation(),
                ResourceId: "test-patient-one-id",
                TimeStamp: timeStamp);
            
            //Act
            FhirOptionalResourceResponse response = await target.Handle(request: fhirUpdateRequest, cancellationToken: cancellationTokenSource.Token);

            //Verify
            _resourceStoreGetForUpdateByResourceIdMock.Verify(x => 
                    x.Get(
                        It.Is<FhirResourceTypeId>(p => 
                            p.Equals(FhirResourceTypeId.Observation)),
                        It.IsAny<string>()),
                times: Times.Once);
            
            
            _preferredReturnTypeServiceMock.Verify(x => 
                x.GetResponse(
                    It.Is<HttpStatusCode>(s => s.Equals(HttpStatusCode.OK)), 
                    It.IsAny<Resource>(), 
                    It.IsAny<int>(),
                    It.IsAny<Dictionary<string, StringValues>>(),
                    It.IsAny<Dictionary<string, StringValues>>(),
                     It.IsAny<IRepositoryEventCollector>() )
                , times: Times.Once);
            
            //Assert
            Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
            Assert.NotNull(response.Resource);
            Assert.IsType<Observation>(response.Resource);
            Observation? observation = response.Resource as Observation;
            Assert.Equal("2", observation!.VersionId);
            Assert.NotNull(response.Headers);
           
            Assert.Equal(_now.ToString("r"), response.Headers[HttpHeaderName.LastModified]);
            Assert.Equal("2", response.Headers[HttpHeaderName.ETag]);
            Assert.Equal(_now.ToString("r"), response.Headers[HttpHeaderName.Date]);
        }
        
        [Fact]
        public async Task updateAsCreate_IsCreated()
        {
            //Arrange
            
            //Lookup returns no resource to force create
            var resourceStoreGetForUpdateByResourceIdMock = new Mock<IResourceStoreGetForUpdateByResourceId>();
            resourceStoreGetForUpdateByResourceIdMock.Setup(x => 
                    x.Get(
                        It.IsAny<FhirResourceTypeId>(), 
                        It.IsAny<string>()))
                .ReturnsAsync((ResourceStoreUpdateProjection?)null); 
            
            var target = new FhirUpdateHandler(
                _validatorMock.Object,
                _fhirResourceTypeSupport,
                resourceStoreGetForUpdateByResourceIdMock.Object,
                _fhirCreateHandlerMock.Object,
                _resourceStoreAddMock.Object,
                _indexer,
                _fhirSerializationSupport,
                _resourceStoreUpdate,
                _fhirResponseHttpHeaderSupport,
                _fhirRequestHttpHeaderSupport,
                _operationOutcomeSupport,
                _preferredReturnTypeServiceMock.Object,
                _indexingSettingsOptionsMock.Object,
                _repositoryEventCollectorMock.Object);
                
            var cancellationTokenSource = new CancellationTokenSource();

            var timeStamp = DateTimeOffset.Now;
            var fhirUpdateRequest = new FhirUpdateRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: "Request Id",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>(),
                ResourceName: ResourceType.Observation.GetLiteral(),
                Resource: new Observation() { Id = "test-patient-one-id"},
                ResourceId: "test-patient-one-id",
                TimeStamp: timeStamp);
            
            //Act
            FhirOptionalResourceResponse response = await target.Handle(request: fhirUpdateRequest, cancellationToken: cancellationTokenSource.Token);
            
            //Verify
            resourceStoreGetForUpdateByResourceIdMock.Verify(x => 
                x.Get(
                    It.IsAny<FhirResourceTypeId>(),
                    It.Is<string>( v => v.Equals(fhirUpdateRequest.ResourceId))),
                times: Times.Once);
            
            _fhirCreateHandlerMock.Verify(x => 
                x.Handle(
                    It.IsAny<FhirCreateRequest>(), 
                    It.IsAny<CancellationToken>()), 
                times: Times.Once);
            
            
            //Assert
            Assert.Equal(HttpStatusCode.Created, response.HttpStatusCode);
            Assert.NotNull(response.Resource);
            Assert.IsType<Observation>(response.Resource);
            Observation? observation = response.Resource as Observation;
            Assert.Equal("1", observation!.VersionId);
            Assert.NotNull(response.Headers);
           
            //Assert.Equal(Now.ToString("r"), response.Headers[HttpHeaderName.LastModified]);
            Assert.Equal("1", response.Headers[HttpHeaderName.ETag]);
            Assert.Equal(_now.ToString("r"), response.Headers[HttpHeaderName.Date]);
        }
        
        
        [Fact]
        public async Task Update_IfMatch_IsPreconditionFailure()
        {
            //Arrange
            var target = new FhirUpdateHandler(
                _validatorMock.Object,
                _fhirResourceTypeSupport,
                _resourceStoreGetForUpdateByResourceIdMock.Object,
                _fhirCreateHandlerMock.Object,
                _resourceStoreAddMock.Object,
                _indexer,
                _fhirSerializationSupport,
                _resourceStoreUpdate,
                _fhirResponseHttpHeaderSupport,
                _fhirRequestHttpHeaderSupport,
                _operationOutcomeSupport,
                _preferredReturnTypeServiceMock.Object,
                _indexingSettingsOptionsMock.Object,
                _repositoryEventCollectorMock.Object);
                
            var cancellationTokenSource = new CancellationTokenSource();

            var timeStamp = DateTimeOffset.Now;
            var fhirUpdateRequest = new FhirUpdateRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: "Request Id",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>()
                {
                    {HttpHeaderName.IfMatch, new StringValues(StringSupport.GetEtag(5))},//note the version is not equal to that frm the database which is 1        
                },
                ResourceName: ResourceType.Observation.GetLiteral(),
                Resource: new Observation() { Id = "test-patient-one-id"},
                ResourceId: "test-patient-one-id",
                TimeStamp: timeStamp);
            
            //Act
            FhirOptionalResourceResponse response = await target.Handle(
                request: fhirUpdateRequest, 
                cancellationToken: cancellationTokenSource.Token);

            //Verify
            _resourceStoreGetForUpdateByResourceIdMock.Verify(x => 
                    x.Get(
                        It.Is<FhirResourceTypeId>(p => 
                            p.Equals(FhirResourceTypeId.Observation)),
                        It.Is<string>( v => v.Equals(fhirUpdateRequest.ResourceId))),
                times: Times.Once);
            
            //Assert
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.HttpStatusCode);
            Assert.NotNull(response.Resource);
            Assert.IsType<OperationOutcome>(response.Resource);
            Assert.NotNull(response.Headers);
            
        }
        
    }
    
}