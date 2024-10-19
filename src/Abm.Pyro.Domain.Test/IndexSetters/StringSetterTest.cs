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

public class StringSetterTest
{
    //Setup
    protected StringSetterTest()
    {
    
    }

    public class Set : StringSetterTest
    {
        [Theory]
        [InlineData( "Frank Herbert", "frank herbert")]
        [InlineData( "    one tWo  ThRee   ", "one two  three")]
        [InlineData( "Remove Diacritics:  ϕ Ϣ ϻ ύ ϋ Ϋ Ё Ѐ", "remove diacritics:  ϕ ϣ ϻ υ υ υ е е")]
        public void String_IsOk(string value, string expected)
        {
            //Arrange
            var target = new StringSetter();
            
            Patient patientResource = TestResourceFactory.PatientResource.GetDonaldDuck();
            
            patientResource.Name.First().Family = value;
            var typedElementList = GetTypedElementList("Patient.name.family", patientResource);

            foreach (var typedElement in typedElementList)
            {
                //Act
                IList<IndexString> indexList = target.Set(typedElement: typedElement, resourceType: FhirResourceTypeId.Patient, searchParameterId: 1, searchParameterName: "the-search-parameter-code");
                
                //Assert
                Assert.Single(indexList);
                Assert.Equal(expected, indexList.First().Value);
            }
        }
        
        private IEnumerable<ITypedElement> GetTypedElementList(string expression,
            Patient patientResource)
        {
            ScopedNode resourceModel = new ScopedNode(patientResource.ToTypedElement());
                
            var fhirPathResolveMock = new Mock<IFhirPathResolve>();

            IEnumerable<ITypedElement> typedElementList = resourceModel.Select(
                expression: expression,
                ctx: new FhirEvaluationContext()
                {
                    ElementResolver = fhirPathResolveMock.Object.Resolver
                });
            return typedElementList;
        }
    }
}