using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
using Abm.Pyro.Application.Notification;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;
using Xunit;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.Test.FhirHandler;

public class FhirReadHandlerTest
{
    private readonly Mock<IValidator> ValidatorMock;
    private readonly IResourceStoreGetByResourceId ResourceStoreGetByResourceId;
    private readonly IFhirResponseHttpHeaderSupport FhirResponseHttpHeaderSupport;
    private readonly IFhirRequestHttpHeaderSupport FhirRequestHttpHeaderSupport;
    private readonly IFhirResourceTypeSupport FhirResourceTypeSupport;
    private readonly IFhirDeSerializationSupport FhirDeSerializationSupport;
    private readonly Mock<IRepositoryEventCollector> RepositoryEventCollectorMock;

    //Setup
    protected FhirReadHandlerTest()
    {
        var patientResource = new Patient()
        {
            Id = "test-patient-id",
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

        var resourceStore = new ResourceStore(
            resourceStoreId: 1,
            resourceId: patientResource.Id,
            versionId: 10,
            isCurrent: true,
            isDeleted: false,
            resourceType: FhirResourceTypeId.Patient,
            httpVerb: HttpVerbId.Delete,
            json: patientResource.ToJson(),
            lastUpdatedUtc: new DateTime(2023, 01, 01, 10, 00, 00 ), //utc
            indexReferenceList: new List<IndexReference>(),
            indexStringList: new List<IndexString>(),
            indexDateTimeList: new List<IndexDateTime>(),
            indexQuantityList: new List<IndexQuantity>(),
            indexTokenList: new List<IndexToken>(),
            indexUriList: new List<IndexUri>(),
            rowVersion: 100);
        
        ValidatorMock = new Mock<IValidator>();
        ValidatorMock.Setup(x => 
                x.Validate(
                    It.IsAny<IValidatable>()))
            .Returns(new ValidatorResult(isValid: true, httpStatusCode: null,  operationOutcome: null));
        
        var resourceStoreGetByResourceIdMock = new Mock<IResourceStoreGetByResourceId>();
        resourceStoreGetByResourceIdMock.Setup(x => 
            x.Get(It.IsAny<FhirResourceTypeId>(), It.IsAny<string>()))
            .Returns(Task.FromResult((ResourceStore?)resourceStore));
        ResourceStoreGetByResourceId = resourceStoreGetByResourceIdMock.Object;
        
        FhirResponseHttpHeaderSupport = new FhirResponseHttpHeaderSupport();
        FhirResourceTypeSupport = new FhirResourceTypeSupport();
            
        var fhirDeSerializationSupportMock = new Mock<IFhirDeSerializationSupport>();
        fhirDeSerializationSupportMock.Setup(x => 
            x.ToResource(It.IsAny<string>()))
            .Returns(patientResource);
        FhirDeSerializationSupport = fhirDeSerializationSupportMock.Object;
        
        FhirRequestHttpHeaderSupport = new FhirRequestHttpHeaderSupport();

        RepositoryEventCollectorMock = new Mock<IRepositoryEventCollector>();
        RepositoryEventCollectorMock
            .Setup(x => 
                x.Add(It.IsAny<RepositoryEvent>()));
        
    }
    
    
    public class Handle : FhirReadHandlerTest
    {
        [Fact]
        public async Task Read_IsOk()
        {
            //Arrange
            var target = new FhirReadHandler(
                ValidatorMock.Object,
                ResourceStoreGetByResourceId,
                FhirResponseHttpHeaderSupport,
                FhirResourceTypeSupport,
                FhirDeSerializationSupport,
                FhirRequestHttpHeaderSupport,
                RepositoryEventCollectorMock.Object);
            
            var cancellationTokenSource = new CancellationTokenSource();

            var timeStamp = DateTimeOffset.Now;
            var fhirReadRequest = new FhirReadRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: "requestId",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>(),
                ResourceName: ResourceType.Patient.GetLiteral(),
                ResourceId: "test-patient-id",
                TimeStamp: timeStamp);
            
            //Act
            FhirOptionalResourceResponse response = await target.Handle(request: fhirReadRequest, cancellationToken: cancellationTokenSource.Token);


            DateTime lastModified = new DateTime(2023, 01, 01, 10, 00, 00);
            //Assert
            Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
            Assert.NotNull(response.Resource);
            Assert.IsType<Patient>(response.Resource);
            Assert.NotNull(response.Headers);
            Assert.Equal(lastModified.ToString("r"), response.Headers[HttpHeaderName.LastModified]);
            Assert.Equal(StringSupport.GetEtag(10), response.Headers[HttpHeaderName.ETag]);
            Assert.Equal(timeStamp.ToString("r"), response.Headers[HttpHeaderName.Date]);
        }
        
        [Fact]
        public async Task Read_IsNotFound()
        {
            //Arrange
            
            //return no resource from the Database
            var resourceStoreGetByResourceIdMock = new Mock<IResourceStoreGetByResourceId>();
            resourceStoreGetByResourceIdMock.Setup(x => 
                    x.Get(It.IsAny<FhirResourceTypeId>(), It.IsAny<string>()))
                .Returns(Task.FromResult((ResourceStore?)null));
            
            var target = new FhirReadHandler(
                ValidatorMock.Object,
                resourceStoreGetByResourceIdMock.Object,
                FhirResponseHttpHeaderSupport,
                FhirResourceTypeSupport,
                FhirDeSerializationSupport,
                FhirRequestHttpHeaderSupport,
                RepositoryEventCollectorMock.Object);
            
            var cancellationTokenSource = new CancellationTokenSource();

            var timeStamp = DateTimeOffset.Now;
            var fhirReadRequest = new FhirReadRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: "requestId",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>(),
                ResourceName: ResourceType.Patient.GetLiteral(),
                ResourceId: "test-patient-id",
                TimeStamp: timeStamp);
            
            //Act
            FhirOptionalResourceResponse response = await target.Handle(request: fhirReadRequest, cancellationToken: cancellationTokenSource.Token);
            
            //Assert
            Assert.Equal(HttpStatusCode.NotFound, response.HttpStatusCode);
            Assert.Null(response.Resource);
            Assert.False(response.Headers.Any());
        }
        
        [Fact]
        public async Task Read_IsDeleted()
        {
            //Arrange
            
            //return Deleted resource from the Database
            var resourceStore = new ResourceStore(
                resourceStoreId: 1,
                resourceId: "test-patient-id",
                versionId: 10,
                isCurrent: true,
                isDeleted: true,
                resourceType: FhirResourceTypeId.Patient,
                httpVerb: HttpVerbId.Delete,
                json: string.Empty,
                lastUpdatedUtc: new DateTime(2023, 01, 01, 10, 00, 00 ), //utc
                indexReferenceList: new List<IndexReference>(),
                indexStringList: new List<IndexString>(),
                indexDateTimeList: new List<IndexDateTime>(),
                indexQuantityList: new List<IndexQuantity>(),
                indexTokenList: new List<IndexToken>(),
                indexUriList: new List<IndexUri>(),
                rowVersion: 100);
            
            var resourceStoreGetByResourceIdMock = new Mock<IResourceStoreGetByResourceId>();
            resourceStoreGetByResourceIdMock.Setup(x => 
                    x.Get(It.IsAny<FhirResourceTypeId>(), It.IsAny<string>()))
                .Returns(Task.FromResult((ResourceStore?)resourceStore));
            
            var target = new FhirReadHandler(
                ValidatorMock.Object,
                resourceStoreGetByResourceIdMock.Object,
                FhirResponseHttpHeaderSupport,
                FhirResourceTypeSupport,
                FhirDeSerializationSupport,
                FhirRequestHttpHeaderSupport,
                RepositoryEventCollectorMock.Object);
            
            var cancellationTokenSource = new CancellationTokenSource();

            var timeStamp = DateTimeOffset.Now;
            var fhirReadRequest = new FhirReadRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: "requestId",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>(),
                ResourceName: ResourceType.Patient.GetLiteral(),
                ResourceId: "test-patient-id",
                TimeStamp: timeStamp);
            
            //Act
            FhirOptionalResourceResponse response = await target.Handle(request: fhirReadRequest, cancellationToken: cancellationTokenSource.Token);
            
            DateTime lastModified = new DateTime(2023, 01, 01, 10, 00, 00);
            //Assert
            Assert.Equal(HttpStatusCode.Gone, response.HttpStatusCode);
            Assert.Null(response.Resource);
            Assert.True(response.Headers.Any());
            Assert.Equal(lastModified.ToString("r"), response.Headers[HttpHeaderName.LastModified]);
            Assert.Equal(StringSupport.GetEtag(10), response.Headers[HttpHeaderName.ETag]);
            Assert.Equal(timeStamp.ToString("r"), response.Headers[HttpHeaderName.Date]);
        }
        
        [Fact]
        public async Task IfModifiedSince_IsNotModified()
        {
            //Arrange
            var target = new FhirReadHandler(
                ValidatorMock.Object,
                ResourceStoreGetByResourceId,
                FhirResponseHttpHeaderSupport,
                FhirResourceTypeSupport,
                FhirDeSerializationSupport,
                FhirRequestHttpHeaderSupport,
                RepositoryEventCollectorMock.Object);
            
            var cancellationTokenSource = new CancellationTokenSource();

            var IfModifiedSince = new DateTime(2023, 01, 01, 10, 01, 00);
            
            var fhirReadRequest = new FhirReadRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: "requestId",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>()
                {
                    { HttpHeaderName.IfModifiedSince, new StringValues(IfModifiedSince.ToString("r")) }
                },
                ResourceName: ResourceType.Patient.GetLiteral(),
                ResourceId: "test-patient-id",
                TimeStamp: DateTimeOffset.Now);
            
            //Act
            FhirOptionalResourceResponse response = await target.Handle(request: fhirReadRequest, cancellationToken: cancellationTokenSource.Token);
            
            //Assert
            Assert.Equal(HttpStatusCode.NotModified, response.HttpStatusCode);
            
        }
        
        [Fact]
        public async Task IfModifiedSince_IsOk()
        {
            //Arrange
            var target = new FhirReadHandler(
                ValidatorMock.Object,
                ResourceStoreGetByResourceId,
                FhirResponseHttpHeaderSupport,
                FhirResourceTypeSupport,
                FhirDeSerializationSupport,
                FhirRequestHttpHeaderSupport,
                RepositoryEventCollectorMock.Object);
            
            var cancellationTokenSource = new CancellationTokenSource();

            var IfModifiedSince = new DateTime(2023, 01, 01, 09, 00, 00);
            
            var fhirReadRequest = new FhirReadRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: "requestId",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>()
                {
                    { HttpHeaderName.IfModifiedSince, new StringValues(IfModifiedSince.ToString("r")) }
                },
                ResourceName: ResourceType.Patient.GetLiteral(),
                ResourceId: "test-patient-id",
                TimeStamp: DateTimeOffset.Now);
            
            //Act
            FhirOptionalResourceResponse response = await target.Handle(request: fhirReadRequest, cancellationToken: cancellationTokenSource.Token);
            
            //Assert
            Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
            
        }
        
        [Fact]
        public async Task IfNoneMatch_IsNotModified()
        {
            //Arrange
            var target = new FhirReadHandler(
                ValidatorMock.Object,
                ResourceStoreGetByResourceId,
                FhirResponseHttpHeaderSupport,
                FhirResourceTypeSupport,
                FhirDeSerializationSupport,
                FhirRequestHttpHeaderSupport,
                RepositoryEventCollectorMock.Object);
            
            var cancellationTokenSource = new CancellationTokenSource();
            
            var fhirReadRequest = new FhirReadRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: "requestId",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>()
                {
                    { HttpHeaderName.IfNoneMatch, new StringValues(StringSupport.GetEtag(10)) },
                },
                ResourceName: ResourceType.Patient.GetLiteral(),
                ResourceId: "test-patient-id",
                TimeStamp: DateTimeOffset.Now);
            
            //Act
            FhirOptionalResourceResponse response = await target.Handle(request: fhirReadRequest, cancellationToken: cancellationTokenSource.Token);
            
            //Assert
            Assert.Equal(HttpStatusCode.NotModified, response.HttpStatusCode);
            
        }
        
        [Fact]
        public async Task IfNoneMatch_IsOk()
        {
            //Arrange
            var target = new FhirReadHandler(
                ValidatorMock.Object,
                ResourceStoreGetByResourceId,
                FhirResponseHttpHeaderSupport,
                FhirResourceTypeSupport,
                FhirDeSerializationSupport,
                FhirRequestHttpHeaderSupport,
                RepositoryEventCollectorMock.Object);
            
            var cancellationTokenSource = new CancellationTokenSource();
            
            var fhirReadRequest = new FhirReadRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: "requestId",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>()
                {
                    { HttpHeaderName.IfNoneMatch, new StringValues(StringSupport.GetEtag(9)) },
                },
                ResourceName: ResourceType.Patient.GetLiteral(),
                ResourceId: "test-patient-id",
                TimeStamp: DateTimeOffset.Now);
            
            //Act
            FhirOptionalResourceResponse response = await target.Handle(request: fhirReadRequest, cancellationToken: cancellationTokenSource.Token);
            
            //Assert
            Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
            
        }
        
        [Fact]
        public async Task IfNoneMatchAndIfModifiedSince_IsNotModified()
        {
            //Arrange
            var target = new FhirReadHandler(
                ValidatorMock.Object,
                ResourceStoreGetByResourceId,
                FhirResponseHttpHeaderSupport,
                FhirResourceTypeSupport,
                FhirDeSerializationSupport,
                FhirRequestHttpHeaderSupport,
                RepositoryEventCollectorMock.Object);
            
            var cancellationTokenSource = new CancellationTokenSource();

            var IfModifiedSince = new DateTime(2023, 01, 01, 10, 00, 00);
            
            var fhirReadRequest = new FhirReadRequest(
                RequestSchema: "http",
                Tenant: "test-tenant",
                RequestId: "requestId",
                RequestPath: "fhir",
                QueryString: null,
                Headers: new Dictionary<string, StringValues>()
                {
                    { HttpHeaderName.IfNoneMatch, new StringValues(StringSupport.GetEtag(9)) },
                    { HttpHeaderName.IfModifiedSince, new StringValues(IfModifiedSince.ToString("r")) }
                },
                ResourceName: ResourceType.Patient.GetLiteral(),
                ResourceId: "test-patient-id",
                TimeStamp: DateTimeOffset.Now);
            
            //Act
            FhirOptionalResourceResponse response = await target.Handle(request: fhirReadRequest, cancellationToken: cancellationTokenSource.Token);
            
            //Assert
            Assert.Equal(HttpStatusCode.NotModified, response.HttpStatusCode);
            
        }
    }
}