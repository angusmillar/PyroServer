using System;
using System.Collections.Generic;
using System.Linq;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using Microsoft.Extensions.Options;
using Moq;
using Abm.Pyro.Application.FhirResolver;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.IndexSetters;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Test.Factories;
using Xunit;

namespace Abm.Pyro.Domain.Test.IndexSetters;

public class DateTimeSetterTest
{
    //Setup
    protected DateTimeSetterTest()
    {
    
    }

    public class Set : DateTimeSetterTest
    {
        [Fact]
        public void Instant_IsOk()
        {
            var serviceDefaultTimeZoneSettingsOptionsMock = new Mock<IOptions<ServiceDefaultTimeZoneSettings>>();
            serviceDefaultTimeZoneSettingsOptionsMock.Setup(x => x.Value)
                .Returns(new ServiceDefaultTimeZoneSettings()
                {
                   TimeZoneTimeSpan = TimeSpan.FromHours(10)
                });

            var fhirDateTimeFactory = new FhirDateTimeFactory(serviceDefaultTimeZoneSettingsOptionsMock.Object);
            
            IDateTimeIndexSupport dateTimeIndexSupport = new DateTimeIndexSupport(fhirDateTimeFactory, new FhirDateTimeSupport());
            
            //Arrange
            var target = new DateTimeSetter(
                dateTimeIndexSupport,
                fhirDateTimeFactory);

            var lastUpdated = new DateTimeOffset(2023, 10, 05, 10, 00, 00, 000, TimeSpan.FromHours(10));
            Patient patientResource = TestResourceFactory.PatientResource.GetDonaldDuck();
            patientResource.Meta.LastUpdated = lastUpdated;
            ScopedNode resourceModel = new ScopedNode(patientResource.ToTypedElement());

            var FhirPathResolveMock = new Mock<IFhirPathResolve>();
            
            IEnumerable<ITypedElement> typedElementList = resourceModel.Select(
                expression: "Resource.meta.lastUpdated",
                ctx: new FhirEvaluationContext(resourceModel)
                {
                    ElementResolver = FhirPathResolveMock.Object.Resolver 
                });

            foreach (var typedElement in typedElementList)
            {
                IList<IndexDateTime> indexList = target.Set(typedElement: typedElement, resourceType: FhirResourceTypeId.Patient, searchParameterId: 1, searchParameterName: "the-search-parameter-code");
                Assert.Single(indexList);
                Assert.Equal(lastUpdated.ToUniversalTime().DateTime, indexList.First().LowUtc);
                Assert.Equal(lastUpdated.ToUniversalTime().DateTime.AddMilliseconds(999), indexList.First().HighUtc);
            }
            
        }
        
        [Fact]
        public void Date_IsOk()
        {
            var serviceDefaultTimeZoneSettingsOptionsMock = new Mock<IOptions<ServiceDefaultTimeZoneSettings>>();
            serviceDefaultTimeZoneSettingsOptionsMock.Setup(x => x.Value)
                .Returns(new ServiceDefaultTimeZoneSettings()
                {
                   TimeZoneTimeSpan = TimeSpan.FromHours(10)
                });

            var fhirDateTimeFactory = new FhirDateTimeFactory(serviceDefaultTimeZoneSettingsOptionsMock.Object);
            
            IDateTimeIndexSupport dateTimeIndexSupport = new DateTimeIndexSupport(fhirDateTimeFactory, new FhirDateTimeSupport());
            
            //Arrange
            var target = new DateTimeSetter(
                dateTimeIndexSupport,
                fhirDateTimeFactory);

            var dob = new DateTimeOffset(1973, 09, 30, 00, 00, 00, 00, 00, TimeSpan.FromHours(10));
            var birthDate = new Hl7.Fhir.Model.Date(dob.Year, dob.Month, dob.Day);
            Patient patientResource = TestResourceFactory.PatientResource.GetDonaldDuck();
            patientResource.BirthDateElement = birthDate;
            ScopedNode resourceModel = new ScopedNode(patientResource.ToTypedElement());

            var fhirPathResolveMock = new Mock<IFhirPathResolve>();
            
            IEnumerable<ITypedElement> typedElementList = resourceModel.Select(
                expression: "Patient.birthDate",
                ctx: new FhirEvaluationContext(resourceModel)
                {
                    ElementResolver = fhirPathResolveMock.Object.Resolver 
                });

            foreach (var typedElement in typedElementList)
            {
                IList<IndexDateTime> indexList = target.Set(typedElement: typedElement, resourceType: FhirResourceTypeId.Patient, searchParameterId: 1, searchParameterName: "the-search-parameter-code");
                Assert.Single(indexList);
                Assert.Equal(dob.DateTime.ToUniversalTime(), indexList.First().LowUtc);
                Assert.Equal(dob.DateTime.AddDays(1).AddMilliseconds(-1).ToUniversalTime(), indexList.First().HighUtc);
            }
            
        }
        
        [Fact]
        public void DateTime_IsOk()
        {
            var serviceDefaultTimeZoneSettingsOptionsMock = new Mock<IOptions<ServiceDefaultTimeZoneSettings>>();
            serviceDefaultTimeZoneSettingsOptionsMock.Setup(x => x.Value)
                .Returns(new ServiceDefaultTimeZoneSettings()
                {
                   TimeZoneTimeSpan = TimeSpan.FromHours(10)
                });

            var fhirDateTimeFactory = new FhirDateTimeFactory(serviceDefaultTimeZoneSettingsOptionsMock.Object);
            
            IDateTimeIndexSupport dateTimeIndexSupport = new DateTimeIndexSupport(fhirDateTimeFactory, new FhirDateTimeSupport());
            
            //Arrange
            var target = new DateTimeSetter(
                dateTimeIndexSupport,
                fhirDateTimeFactory);

            var deceasedDateTime = new DateTimeOffset(1973, 09, 30, 00, 00, 00, 00, 00, TimeSpan.FromHours(10));
            
            Patient patientResource = TestResourceFactory.PatientResource.GetDonaldDuck();
            patientResource.Deceased = new FhirDateTime(deceasedDateTime);
            ScopedNode resourceModel = new ScopedNode(patientResource.ToTypedElement());

            var fhirPathResolveMock = new Mock<IFhirPathResolve>();
            
            IEnumerable<ITypedElement> typedElementList = resourceModel.Select(
                expression: "(Patient.deceased as dateTime)",
                ctx: new FhirEvaluationContext(resourceModel)
                {
                    ElementResolver = fhirPathResolveMock.Object.Resolver 
                });

            foreach (var typedElement in typedElementList)
            {
                IList<IndexDateTime> indexList = target.Set(typedElement: typedElement, resourceType: FhirResourceTypeId.Patient, searchParameterId: 1, searchParameterName: "the-search-parameter-code");
                Assert.Single(indexList);
                Assert.Equal(deceasedDateTime.DateTime.ToUniversalTime(), indexList.First().LowUtc);
                Assert.Equal(deceasedDateTime.DateTime.AddMilliseconds(999).ToUniversalTime(), indexList.First().HighUtc);
            }
            
        }
        
           [Fact]
        public void Period_IsOk()
        {
            var serviceDefaultTimeZoneSettingsOptionsMock = new Mock<IOptions<ServiceDefaultTimeZoneSettings>>();
            serviceDefaultTimeZoneSettingsOptionsMock.Setup(x => x.Value)
                .Returns(new ServiceDefaultTimeZoneSettings()
                {
                   TimeZoneTimeSpan = TimeSpan.FromHours(10)
                });

            var fhirDateTimeFactory = new FhirDateTimeFactory(serviceDefaultTimeZoneSettingsOptionsMock.Object);
            
            IDateTimeIndexSupport dateTimeIndexSupport = new DateTimeIndexSupport(fhirDateTimeFactory, new FhirDateTimeSupport());
            
            //Arrange
            var target = new DateTimeSetter(
                dateTimeIndexSupport,
                fhirDateTimeFactory);

            var effectiveStartDate = new DateTimeOffset(2023, 09, 01, 00, 00, 00, 00, 00, TimeSpan.FromHours(10));
            var effectiveEndDate = new DateTimeOffset(2023, 09, 02, 00, 00, 00, 00, 00, TimeSpan.FromHours(10));

            Observation hemoglobinObservation = TestResourceFactory.ObservationResource.GetHemoglobinObservation();
            hemoglobinObservation.Effective = new Period(start: new FhirDateTime(effectiveStartDate), end: new FhirDateTime(effectiveEndDate));
            ScopedNode resourceModel = new ScopedNode(hemoglobinObservation.ToTypedElement());

            var fhirPathResolveMock = new Mock<IFhirPathResolve>();
            
            IEnumerable<ITypedElement> typedElementList = resourceModel.Select(
                expression: "Observation.effective",
                ctx: new FhirEvaluationContext(resourceModel)
                {
                    ElementResolver = fhirPathResolveMock.Object.Resolver 
                });

            foreach (var typedElement in typedElementList)
            {
                IList<IndexDateTime> indexList = target.Set(typedElement: typedElement, resourceType: FhirResourceTypeId.Observation, searchParameterId: 1, searchParameterName: "the-search-parameter-code");
                Assert.Single(indexList);
                Assert.Equal(effectiveStartDate.DateTime.ToUniversalTime(), indexList.First().LowUtc);
                Assert.Equal(effectiveEndDate.DateTime.AddMilliseconds(999).ToUniversalTime(), indexList.First().HighUtc);
            }
            
        }
    }
}