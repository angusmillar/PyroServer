using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using MediatR;
using Microsoft.Extensions.Primitives;
using Moq;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.Indexing;
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

public class FhirCreateHandlerTest
{
    private readonly Mock<IValidator> ValidatorMock;
    private readonly Mock<IResourceStoreAdd> ResourceStoreAddMock;
    private readonly Mock<IFhirSerializationSupport> FhirSerializationSupportMock;
    private readonly IFhirResourceTypeSupport FhirResourceTypeSupport;
    private readonly IFhirResponseHttpHeaderSupport FhirResponseHttpHeaderSupport;
    private readonly Mock<IIndexer> IndexerMock;
    private readonly Mock<IPreferredReturnTypeService> PreferredReturnTypeServiceMock;


    private readonly DateTime Now;

    //Setup
    protected FhirCreateHandlerTest()
    {
        Now = DateTime.Now;
        
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

        ValidatorMock = new Mock<IValidator>();
        ValidatorMock.Setup(x => 
            x.Validate(
                It.IsAny<IValidatable>()))
            .Returns(new ValidatorResult(isValid: true, httpStatusCode: null, operationOutcome: null));
        
        ResourceStoreAddMock = new Mock<IResourceStoreAdd>();
        ResourceStoreAddMock.Setup(x =>
                x.Add(
                    It.IsAny<ResourceStore>()))
            .ReturnsAsync((ResourceStore resourceStore) => resourceStoreFromDbAdd);
        
        FhirResourceTypeSupport = new FhirResourceTypeSupport();

        FhirSerializationSupportMock = new Mock<IFhirSerializationSupport>();
        FhirSerializationSupportMock.Setup(x =>
                x.ToJson(
                    It.IsAny<Resource>(),
                    It.IsAny<Hl7.Fhir.Rest.SummaryType?>(),
                    It.IsAny<bool>()))
            .Returns("The Observation resource's JSON string would be here, but why bother for the unit test!");


        FhirResponseHttpHeaderSupport = new FhirResponseHttpHeaderSupport();

        var indexerOutcome = new IndexerOutcome(
            stringIndexList: new List<IndexString>(),
            referenceIndexList: new List<IndexReference>(),
            dateTimeIndexList: new List<IndexDateTime>(),
            quantityIndexList: new List<IndexQuantity>(),
            tokenIndexList: new List<IndexToken>(),
            uriIndexList: new List<IndexUri>());

        IndexerMock = new Mock<IIndexer>();
        IndexerMock.Setup(x =>
                x.Process(
                    It.IsAny<Resource>(),
                    It.IsAny<FhirResourceTypeId>()))
            .ReturnsAsync(indexerOutcome);

        var responseObservationResource = GetObservationResource();
        var fhirOptionalResourceResponse = new FhirOptionalResourceResponse(
            Resource: responseObservationResource,
            HttpStatusCode: HttpStatusCode.Created,
            Headers: new Dictionary<string, StringValues>());

        PreferredReturnTypeServiceMock = new Mock<IPreferredReturnTypeService>();
        PreferredReturnTypeServiceMock
            .Setup(x =>
                x.GetResponse(
                    It.IsAny<HttpStatusCode>(),
                    It.IsAny<Resource>(),
                    It.IsAny<int>(),
                    It.IsAny<Dictionary<string, StringValues>>(),
                    It.IsAny<Dictionary<string, StringValues>>()))
            .Returns(fhirOptionalResourceResponse);
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
                ValidatorMock.Object,
                ResourceStoreAddMock.Object,
                FhirSerializationSupportMock.Object,
                FhirResourceTypeSupport,
                FhirResponseHttpHeaderSupport,
                IndexerMock.Object,
                PreferredReturnTypeServiceMock.Object
            );

            var cancellationTokenSource = new CancellationTokenSource();

            Observation observationResourceToPost = GetObservationResource();
            
            var timeStamp = DateTimeOffset.Now;
            var fhirUpdateRequest = new FhirCreateRequest(
                RequestSchema: "http",
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
            ResourceStoreAddMock.Verify(x => 
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
            
            PreferredReturnTypeServiceMock.Verify(x =>
                x.GetResponse(
                    It.Is<HttpStatusCode>(s => s.Equals(HttpStatusCode.Created)),
                    It.IsAny<Resource>(),
                    It.IsAny<int>(),
                    It.IsAny<Dictionary<string, StringValues>>(),
                    It.IsAny<Dictionary<string, StringValues>>()),
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