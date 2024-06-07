using System;
using System.Collections.Generic;
using System.Linq;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Moq;
using Abm.Pyro.Application.FhirResolver;
using Abm.Pyro.Application.Indexing;
using Abm.Pyro.Domain.Test.Factories;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.IndexSetters;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Xunit;
using SearchParamType = Abm.Pyro.Domain.Enums.SearchParamType;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.Test.Indexing;

public class IndexerTest
{
    private readonly Mock<IDateTimeSetter> DateTimeSetterMock;
    private readonly Mock<INumberSetter> NumberSetterMock;
    private readonly Mock<IReferenceSetter> ReferenceSetterSupportMock;
    private readonly Mock<IStringSetter> StringSetterMock;
    private readonly Mock<ITokenSetter> TokenSetterMock;
    private readonly Mock<IQuantitySetter> QuantitySetterMock;
    private readonly Mock<IUriSetter> UriSetterMock;
    private readonly Mock<ILogger<Indexer>> LoggerMock;
    private readonly Mock<ISearchParameterCache> SearchParameterCacheMock;
    private readonly Mock<IFhirPathResolve> FhirPathResolveMock;

    //Setup
    protected IndexerTest()
    {
        SearchParameterCacheMock = new Mock<ISearchParameterCache>();
        DateTimeSetterMock = new Mock<IDateTimeSetter>();
        NumberSetterMock = new Mock<INumberSetter>();
        ReferenceSetterSupportMock = new Mock<IReferenceSetter>();
        StringSetterMock = new Mock<IStringSetter>();
        TokenSetterMock = new Mock<ITokenSetter>();
        QuantitySetterMock = new Mock<IQuantitySetter>();
        UriSetterMock = new Mock<IUriSetter>();
        LoggerMock = Mock.Of<Mock<ILogger<Indexer>>>();
        FhirPathResolveMock = new Mock<IFhirPathResolve>();
    }


    public class Process : IndexerTest
    {
        [Fact]
        public async Task Process_DateTimeIndex()
        {
            //Arrange
            var searchParameter = new SearchParameterProjection(
                searchParameterStoreId: 57,
                code: "date",
                status: PublicationStatusId.Active,
                isCurrent: true,
                isDeleted: false,
                url: new Uri("http://hl7.org/fhir/SearchParameter/clinical-date"),
                type: SearchParamType.Date,
                expression:
                "AllergyIntolerance.recordedDate | CarePlan.period | CareTeam.period | ClinicalImpression.date | Composition.date | Consent.dateTime | DiagnosticReport.effective | Encounter.period | EpisodeOfCare.period | FamilyMemberHistory.date | Flag.period | Immunization.occurrence | List.date | Observation.effective | Procedure.performed | (RiskAssessment.occurrence as dateTime) | SupplyRequest.authoredOn",
                multipleOr: null,
                multipleAnd: null,
                baseList: new List<SearchParameterStoreResourceTypeBase>(),
                targetList: new List<SearchParameterStoreResourceTypeTarget>(),
                comparatorList: new List<SearchParameterStoreComparator>(),
                modifierList: new List<SearchParameterStoreSearchModifierCode>(),
                componentList: new List<SearchParameterStoreComponent>());

            var searchParameterList = new List<SearchParameterProjection>()
            {
                searchParameter
            };

            SearchParameterCacheMock.Setup(x =>
                    x.GetListByResourceType(FhirResourceTypeId.Observation))
                .ReturnsAsync(searchParameterList);

            List<IndexDateTime> dateTimeIndexList = new List<IndexDateTime>()
            {
                new IndexDateTime(indexDateTimeId: 1,
                    resourceStoreId: 100,
                    resourceStore: null,
                    searchParameterStoreId: 100,
                    searchParameterStore: null,
                    lowUtc: new DateTime(2023, 10, 23, 09, 00, 00),
                    highUtc: new DateTime(2023, 10, 23, 09, 30, 00))
            };

            DateTimeSetterMock.Setup(x =>
                x.Set(
                    It.IsAny<ITypedElement>(),
                    It.IsAny<FhirResourceTypeId>(),
                    It.IsAny<int>(),
                    It.IsAny<string>())).Returns(dateTimeIndexList);

            var target = new Indexer(
                DateTimeSetterMock.Object,
                NumberSetterMock.Object,
                ReferenceSetterSupportMock.Object,
                StringSetterMock.Object,
                TokenSetterMock.Object,
                QuantitySetterMock.Object,
                UriSetterMock.Object,
                LoggerMock.Object,
                SearchParameterCacheMock.Object,
                FhirPathResolveMock.Object);

            Observation hemoglobinObservation = TestResourceFactory.ObservationResource.GetHemoglobinObservation();

            //Act
            IndexerOutcome indexerOutcome = await target.Process(fhirResource: hemoglobinObservation, resourceType: FhirResourceTypeId.Observation);

            //Verify
            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Resource))),
                times: Times.Once);

            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Observation))),
                times: Times.Once);

            DateTimeSetterMock.Verify(x =>
                    x.Set(
                        It.IsAny<ITypedElement>(),
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Observation)),
                        It.Is<int>(z => z == searchParameter.SearchParameterStoreId!.Value),
                        It.Is<string>(z => z.Equals(searchParameter.Code))),
                times: Times.Once);

            //Assert
            Assert.Single(indexerOutcome.DateTimeIndexList);
            Assert.Equal(dateTimeIndexList.First().LowUtc, indexerOutcome.DateTimeIndexList.First().LowUtc);
            Assert.Equal(dateTimeIndexList.First().HighUtc, indexerOutcome.DateTimeIndexList.First().HighUtc);
        }

        [Fact]
        public async Task Process_NumberIndex()
        {
            
            //Arrange
            var searchParameter = new SearchParameterProjection(
                searchParameterStoreId: 57,
                code: "number",
                status: PublicationStatusId.Active,
                isCurrent: true,
                isDeleted: false,
                url: new Uri("http://hl7.org/fhir/SearchParameter/clinical-date"),
                type: SearchParamType.Number,
                expression:
                "AllergyIntolerance.recordedDate | CarePlan.period | CareTeam.period | ClinicalImpression.date | Composition.date | Consent.dateTime | DiagnosticReport.effective | Encounter.period | EpisodeOfCare.period | FamilyMemberHistory.date | Flag.period | Immunization.occurrence | List.date | Observation.effective | Procedure.performed | (RiskAssessment.occurrence as dateTime) | SupplyRequest.authoredOn",
                multipleOr: null,
                multipleAnd: null,
                baseList: new List<SearchParameterStoreResourceTypeBase>(),
                targetList: new List<SearchParameterStoreResourceTypeTarget>(),
                comparatorList: new List<SearchParameterStoreComparator>(),
                modifierList: new List<SearchParameterStoreSearchModifierCode>(),
                componentList: new List<SearchParameterStoreComponent>());

            var searchParameterList = new List<SearchParameterProjection>()
            {
                searchParameter
            };

            SearchParameterCacheMock.Setup(x =>
                    x.GetListByResourceType(FhirResourceTypeId.Observation))
                .ReturnsAsync(searchParameterList);

            var quantityIndexList = new List<IndexQuantity>()
            {
                new IndexQuantity(indexQuantityId: 1,
                    resourceStoreId: 100,
                    resourceStore: null,
                    searchParameterStoreId: 100,
                    searchParameterStore: null,
                    comparator: null,
                    quantity: 10.5m,
                    code: "code",
                    system: "https://someSystem",
                    unit: "ml",
                    comparatorHigh: null,
                    quantityHigh: 20.5m,
                    codeHigh: "code",
                    systemHigh: "https://someSystem",
                    unitHigh: "ml")
            };

            NumberSetterMock.Setup(x =>
                x.Set(
                    It.IsAny<ITypedElement>(),
                    It.IsAny<FhirResourceTypeId>(),
                    It.IsAny<int>(),
                    It.IsAny<string>())).Returns(quantityIndexList);

            var target = new Indexer(
                DateTimeSetterMock.Object,
                NumberSetterMock.Object,
                ReferenceSetterSupportMock.Object,
                StringSetterMock.Object,
                TokenSetterMock.Object,
                QuantitySetterMock.Object,
                UriSetterMock.Object,
                LoggerMock.Object,
                SearchParameterCacheMock.Object,
                FhirPathResolveMock.Object);

            Observation hemoglobinObservation = TestResourceFactory.ObservationResource.GetHemoglobinObservation();

            //Act
            IndexerOutcome indexerOutcome = await target.Process(fhirResource: hemoglobinObservation, resourceType: FhirResourceTypeId.Observation);

            //Verify
            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Resource))),
                times: Times.Once);

            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Observation))),
                times: Times.Once);

            NumberSetterMock.Verify(x =>
                    x.Set(
                        It.IsAny<ITypedElement>(),
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Observation)),
                        It.Is<int>(z => z == searchParameter.SearchParameterStoreId!.Value),
                        It.Is<string>(z => z.Equals(searchParameter.Code))),
                times: Times.Once);

            //Assert
            Assert.Single(indexerOutcome.QuantityIndexList);
            Assert.Equal(quantityIndexList.First().Code, indexerOutcome.QuantityIndexList.First().Code);
            Assert.Equal(quantityIndexList.First().CodeHigh, indexerOutcome.QuantityIndexList.First().CodeHigh);
        }

        [Fact]
        public async Task Process_ReferenceIndex()
        {
            //Arrange
            var searchParameter = new SearchParameterProjection(
                searchParameterStoreId: 57,
                code: "patient",
                status: PublicationStatusId.Active,
                isCurrent: true,
                isDeleted: false,
                url: new Uri("http://hl7.org/fhir/SearchParameter/clinical-patient"),
                type: SearchParamType.Reference,
                expression:
                "AllergyIntolerance.patient | CarePlan.subject.where(resolve() is Patient) | CareTeam.subject.where(resolve() is Patient) | ClinicalImpression.subject.where(resolve() is Patient) | Composition.subject.where(resolve() is Patient) | Condition.subject.where(resolve() is Patient) | Consent.patient | DetectedIssue.patient | DeviceRequest.subject.where(resolve() is Patient) | DeviceUseStatement.subject | DiagnosticReport.subject.where(resolve() is Patient) | DocumentManifest.subject.where(resolve() is Patient) | DocumentReference.subject.where(resolve() is Patient) | Encounter.subject.where(resolve() is Patient) | EpisodeOfCare.patient | FamilyMemberHistory.patient | Flag.subject.where(resolve() is Patient) | Goal.subject.where(resolve() is Patient) | ImagingStudy.subject.where(resolve() is Patient) | Immunization.patient | List.subject.where(resolve() is Patient) | MedicationAdministration.subject.where(resolve() is Patient) | MedicationDispense.subject.where(resolve() is Patient) | MedicationRequest.subject.where(resolve() is Patient) | MedicationStatement.subject.where(resolve() is Patient) | NutritionOrder.patient | Observation.subject.where(resolve() is Patient) | Procedure.subject.where(resolve() is Patient) | RiskAssessment.subject.where(resolve() is Patient) | ServiceRequest.subject.where(resolve() is Patient) | SupplyDelivery.patient | VisionPrescription.patient",
                multipleOr: null,
                multipleAnd: null,
                baseList: new List<SearchParameterStoreResourceTypeBase>(),
                targetList: new List<SearchParameterStoreResourceTypeTarget>(),
                comparatorList: new List<SearchParameterStoreComparator>(),
                modifierList: new List<SearchParameterStoreSearchModifierCode>(),
                componentList: new List<SearchParameterStoreComponent>());

            var searchParameterList = new List<SearchParameterProjection>()
            {
                searchParameter
            };

            SearchParameterCacheMock.Setup(x =>
                    x.GetListByResourceType(FhirResourceTypeId.Observation))
                .ReturnsAsync(searchParameterList);

            var referenceIndexList = new List<IndexReference>()
            {
                new IndexReference(indexReferenceId: 1,
                    resourceStoreId: 100,
                    resourceStore: null,
                    searchParameterStoreId: 100,
                    searchParameterStore: null,
                    serviceBaseUrlId: 1,
                    serviceBaseUrl: null,
                    resourceType: FhirResourceTypeId.Patient,
                    resourceId: "resource-id",
                    versionId: "1",
                    canonicalVersionId: "")
            };

            ReferenceSetterSupportMock.Setup(x =>
                x.SetAsync(
                    It.IsAny<ITypedElement>(),
                    It.IsAny<FhirResourceTypeId>(),
                    It.IsAny<int>(),
                    It.IsAny<string>())).ReturnsAsync(referenceIndexList);

            FhirUriFactory fhirUriFactory = FhirUriFactoryFactory.GetFhirUriFactory("https://some-test-base.com/fhir");
            FhirPathResolve fhirPathResolve = new FhirPathResolve(fhirUriFactory: fhirUriFactory);
            
            var target = new Indexer(
                DateTimeSetterMock.Object,
                NumberSetterMock.Object,
                ReferenceSetterSupportMock.Object,
                StringSetterMock.Object,
                TokenSetterMock.Object,
                QuantitySetterMock.Object,
                UriSetterMock.Object,
                LoggerMock.Object,
                SearchParameterCacheMock.Object,
                fhirPathResolve);

            Observation hemoglobinObservation = TestResourceFactory.ObservationResource.GetHemoglobinObservation(
                patientResourceReference: new ResourceReference("Patient/100"));

            //Act
            IndexerOutcome indexerOutcome = await target.Process(fhirResource: hemoglobinObservation, resourceType: FhirResourceTypeId.Observation);

            //Verify
            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Resource))),
                times: Times.Once);

            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Observation))),
                times: Times.Once);

            ReferenceSetterSupportMock.Verify(x =>
                    x.SetAsync(
                        It.IsAny<ITypedElement>(),
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Observation)),
                        It.Is<int>(z => z == searchParameter.SearchParameterStoreId!.Value),
                        It.Is<string>(z => z.Equals(searchParameter.Code))),
                times: Times.Once);

            //Assert
            Assert.Single(indexerOutcome.ReferenceIndexList);
            Assert.Equal(referenceIndexList.First().ResourceId, indexerOutcome.ReferenceIndexList.First().ResourceId);
        }

        [Fact]
        public async Task Process_StringIndex()
        {
            //Arrange
            var searchParameter = new SearchParameterProjection(
                searchParameterStoreId: 100,
                code: "name",
                status: PublicationStatusId.Active,
                isCurrent: true,
                isDeleted: false,
                url: new Uri("http://hl7.org/fhir/SearchParameter/Patient-name"),
                type: SearchParamType.String,
                expression: "Patient.name",
                multipleOr: null,
                multipleAnd: null,
                baseList: new List<SearchParameterStoreResourceTypeBase>(),
                targetList: new List<SearchParameterStoreResourceTypeTarget>(),
                comparatorList: new List<SearchParameterStoreComparator>(),
                modifierList: new List<SearchParameterStoreSearchModifierCode>(),
                componentList: new List<SearchParameterStoreComponent>());

            var searchParameterList = new List<SearchParameterProjection>()
            {
                searchParameter
            };

            SearchParameterCacheMock.Setup(x =>
                    x.GetListByResourceType(FhirResourceTypeId.Patient))
                .ReturnsAsync(searchParameterList);

            List<IndexString> stringIndexList = new List<IndexString>()
            {
                new IndexString(indexStringId: 1,
                    resourceStoreId: 100,
                    resourceStore: null,
                    searchParameterStoreId: 100,
                    searchParameterStore: null,
                    value: "SomeTestValue")
            };

            StringSetterMock.Setup(x =>
                x.Set(
                    It.IsAny<ITypedElement>(),
                    It.IsAny<FhirResourceTypeId>(),
                    It.IsAny<int>(),
                    It.IsAny<string>())).Returns(stringIndexList);

            var target = new Indexer(
                DateTimeSetterMock.Object,
                NumberSetterMock.Object,
                ReferenceSetterSupportMock.Object,
                StringSetterMock.Object,
                TokenSetterMock.Object,
                QuantitySetterMock.Object,
                UriSetterMock.Object,
                LoggerMock.Object,
                SearchParameterCacheMock.Object,
                FhirPathResolveMock.Object);

            Patient patient = TestResourceFactory.PatientResource.GetDonaldDuck();

            //Act
            IndexerOutcome indexerOutcome = await target.Process(fhirResource: patient, resourceType: FhirResourceTypeId.Patient);

            //Verify
            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Resource))),
                times: Times.Once);

            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Patient))),
                times: Times.Once);

            StringSetterMock.Verify(x =>
                    x.Set(
                        It.IsAny<ITypedElement>(),
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Patient)),
                        It.Is<int>(z => z == searchParameter.SearchParameterStoreId!.Value),
                        It.Is<string>(z => z.Equals(searchParameter.Code))),
                times: Times.Once);

            //Assert
            Assert.Single(indexerOutcome.StringIndexList);
            Assert.Equal(stringIndexList.First().Value, indexerOutcome.StringIndexList.First().Value);
        }
        
        [Fact]
        public async Task Process_TokenIndex()
        {
            //Arrange
            var searchParameter = new SearchParameterProjection(
                searchParameterStoreId: 100,
                code: "code",
                status: PublicationStatusId.Active,
                isCurrent: true,
                isDeleted: false,
                url: new Uri("http://hl7.org/fhir/SearchParameter/clinical-code"),
                type: SearchParamType.Token,
                expression: "AllergyIntolerance.code | AllergyIntolerance.reaction.substance | Condition.code | (DeviceRequest.code as CodeableConcept) | DiagnosticReport.code | FamilyMemberHistory.condition.code | List.code | Medication.code | (MedicationAdministration.medication as CodeableConcept) | (MedicationDispense.medication as CodeableConcept) | (MedicationRequest.medication as CodeableConcept) | (MedicationStatement.medication as CodeableConcept) | Observation.code | Procedure.code | ServiceRequest.code",
                multipleOr: null,
                multipleAnd: null,
                baseList: new List<SearchParameterStoreResourceTypeBase>(),
                targetList: new List<SearchParameterStoreResourceTypeTarget>(),
                comparatorList: new List<SearchParameterStoreComparator>(),
                modifierList: new List<SearchParameterStoreSearchModifierCode>(),
                componentList: new List<SearchParameterStoreComponent>());

            var searchParameterList = new List<SearchParameterProjection>()
            {
                searchParameter
            };

            SearchParameterCacheMock.Setup(x =>
                    x.GetListByResourceType(FhirResourceTypeId.Observation))
                .ReturnsAsync(searchParameterList);

            List<IndexToken> stringIndexList = new List<IndexToken>()
            {
                new IndexToken(indexTokenId: 1,
                    resourceStoreId: 100,
                    resourceStore: null,
                    searchParameterStoreId: 100,
                    searchParameterStore: null,
                    code: "Code",
                    system: "https://some-system")
            };

            TokenSetterMock.Setup(x =>
                x.Set(
                    It.IsAny<ITypedElement>(),
                    It.IsAny<FhirResourceTypeId>(),
                    It.IsAny<int>(),
                    It.IsAny<string>())).Returns(stringIndexList);

            var target = new Indexer(
                DateTimeSetterMock.Object,
                NumberSetterMock.Object,
                ReferenceSetterSupportMock.Object,
                StringSetterMock.Object,
                TokenSetterMock.Object,
                QuantitySetterMock.Object,
                UriSetterMock.Object,
                LoggerMock.Object,
                SearchParameterCacheMock.Object,
                FhirPathResolveMock.Object);

            Observation hemoglobinObservation = TestResourceFactory.ObservationResource.GetHemoglobinObservation();

            //Act
            IndexerOutcome indexerOutcome = await target.Process(fhirResource: hemoglobinObservation, resourceType: FhirResourceTypeId.Observation);

            //Verify
            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Resource))),
                times: Times.Once);

            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Observation))),
                times: Times.Once);

            TokenSetterMock.Verify(x =>
                    x.Set(
                        It.IsAny<ITypedElement>(),
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Observation)),
                        It.Is<int>(z => z == searchParameter.SearchParameterStoreId!.Value),
                        It.Is<string>(z => z.Equals(searchParameter.Code))),
                times: Times.Once);

            //Assert
            Assert.Single(indexerOutcome.TokenIndexList);
            Assert.Equal(stringIndexList.First().Code, indexerOutcome.TokenIndexList.First().Code);
        }
        
        [Fact]
        public async Task Process_QuantityIndex()
        {
            
            //Arrange
            var searchParameter = new SearchParameterProjection(
                searchParameterStoreId: 57,
                code: "value-quantity",
                status: PublicationStatusId.Active,
                isCurrent: true,
                isDeleted: false,
                url: new Uri("http://hl7.org/fhir/SearchParameter/Observation-value-quantity"),
                type: SearchParamType.Quantity,
                expression: "(Observation.value as Quantity) | (Observation.value as SampledData)",
                multipleOr: null,
                multipleAnd: null,
                baseList: new List<SearchParameterStoreResourceTypeBase>(),
                targetList: new List<SearchParameterStoreResourceTypeTarget>(),
                comparatorList: new List<SearchParameterStoreComparator>(),
                modifierList: new List<SearchParameterStoreSearchModifierCode>(),
                componentList: new List<SearchParameterStoreComponent>());

            var searchParameterList = new List<SearchParameterProjection>()
            {
                searchParameter
            };

            SearchParameterCacheMock.Setup(x =>
                    x.GetListByResourceType(FhirResourceTypeId.Observation))
                .ReturnsAsync(searchParameterList);

            var quantityIndexList = new List<IndexQuantity>()
            {
                new IndexQuantity(indexQuantityId: 1,
                    resourceStoreId: 100,
                    resourceStore: null,
                    searchParameterStoreId: 100,
                    searchParameterStore: null,
                    comparator: null,
                    quantity: 10.5m,
                    code: "code",
                    system: "https://someSystem",
                    unit: "ml",
                    comparatorHigh: null,
                    quantityHigh: 20.5m,
                    codeHigh: "code",
                    systemHigh: "https://someSystem",
                    unitHigh: "ml")
            };

            QuantitySetterMock.Setup(x =>
                x.Set(
                    It.IsAny<ITypedElement>(),
                    It.IsAny<FhirResourceTypeId>(),
                    It.IsAny<int>(),
                    It.IsAny<string>())).Returns(quantityIndexList);

            var target = new Indexer(
                DateTimeSetterMock.Object,
                NumberSetterMock.Object,
                ReferenceSetterSupportMock.Object,
                StringSetterMock.Object,
                TokenSetterMock.Object,
                QuantitySetterMock.Object,
                UriSetterMock.Object,
                LoggerMock.Object,
                SearchParameterCacheMock.Object,
                FhirPathResolveMock.Object);

            Observation hemoglobinObservation = TestResourceFactory.ObservationResource.GetHemoglobinObservation();

            //Act
            IndexerOutcome indexerOutcome = await target.Process(fhirResource: hemoglobinObservation, resourceType: FhirResourceTypeId.Observation);

            //Verify
            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Resource))),
                times: Times.Once);

            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Observation))),
                times: Times.Once);

            QuantitySetterMock.Verify(x =>
                    x.Set(
                        It.IsAny<ITypedElement>(),
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Observation)),
                        It.Is<int>(z => z == searchParameter.SearchParameterStoreId!.Value),
                        It.Is<string>(z => z.Equals(searchParameter.Code))),
                times: Times.Once);

            //Assert
            Assert.Single(indexerOutcome.QuantityIndexList);
            Assert.Equal(quantityIndexList.First().Code, indexerOutcome.QuantityIndexList.First().Code);
            Assert.Equal(quantityIndexList.First().CodeHigh, indexerOutcome.QuantityIndexList.First().CodeHigh);
        }
        
        [Fact]
        public async Task Process_UriIndex()
        {
            //Arrange
            var searchParameter = new SearchParameterProjection(
                searchParameterStoreId: 100,
                code: "url",
                status: PublicationStatusId.Active,
                isCurrent: true,
                isDeleted: false,
                url: new Uri("http://hl7.org/fhir/SearchParameter/Subscription-url"),
                type: SearchParamType.Uri,
                expression: "Subscription.channel.endpoint",
                multipleOr: null,
                multipleAnd: null,
                baseList: new List<SearchParameterStoreResourceTypeBase>(),
                targetList: new List<SearchParameterStoreResourceTypeTarget>(),
                comparatorList: new List<SearchParameterStoreComparator>(),
                modifierList: new List<SearchParameterStoreSearchModifierCode>(),
                componentList: new List<SearchParameterStoreComponent>());

            var searchParameterList = new List<SearchParameterProjection>()
            {
                searchParameter
            };

            SearchParameterCacheMock.Setup(x =>
                    x.GetListByResourceType(FhirResourceTypeId.Subscription))
                .ReturnsAsync(searchParameterList);

            List<IndexUri> uriIndexList = new List<IndexUri>()
            {
                new IndexUri(indexUriId: 1,
                    resourceStoreId: 100,
                    resourceStore: null,
                    searchParameterStoreId: 100,
                    searchParameterStore: null,
                    uri: "https://some-test-uri")
            };

            UriSetterMock.Setup(x =>
                x.Set(
                    It.IsAny<ITypedElement>(),
                    It.IsAny<FhirResourceTypeId>(),
                    It.IsAny<int>(),
                    It.IsAny<string>())).Returns(uriIndexList);

            var target = new Indexer(
                DateTimeSetterMock.Object,
                NumberSetterMock.Object,
                ReferenceSetterSupportMock.Object,
                StringSetterMock.Object,
                TokenSetterMock.Object,
                QuantitySetterMock.Object,
                UriSetterMock.Object,
                LoggerMock.Object,
                SearchParameterCacheMock.Object,
                FhirPathResolveMock.Object);

            Subscription subscriptionResource = TestResourceFactory.SubscriptionResource.GetSubscription();

            //Act
            IndexerOutcome indexerOutcome = await target.Process(fhirResource: subscriptionResource, resourceType: FhirResourceTypeId.Subscription);

            //Verify
            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Resource))),
                times: Times.Once);

            SearchParameterCacheMock.Verify(x =>
                    x.GetListByResourceType(
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Subscription))),
                times: Times.Once);

            UriSetterMock.Verify(x =>
                    x.Set(
                        It.IsAny<ITypedElement>(),
                        It.Is<FhirResourceTypeId>(z => z.Equals(FhirResourceTypeId.Subscription)),
                        It.Is<int>(z => z == searchParameter.SearchParameterStoreId!.Value),
                        It.Is<string>(z => z.Equals(searchParameter.Code))),
                times: Times.Once);

            //Assert
            Assert.Single(indexerOutcome.UriIndexList);
            Assert.Equal(uriIndexList.First().Uri, indexerOutcome.UriIndexList.First().Uri);
        }
    }
}