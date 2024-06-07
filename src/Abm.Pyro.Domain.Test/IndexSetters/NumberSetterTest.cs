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

public class NumberSetterTest
{
    //Setup
    protected NumberSetterTest()
    {
    
    }

    public class Set : NumberSetterTest
    {
        [Theory]
        [InlineData( 10, 10)]
        [InlineData( 9.999, 9.999)]
        [InlineData( 12345678901234567890, 12345678901234567890)]
        [InlineData( 1.2345678901234567890, 1.2345678901234567890)]
        [InlineData( 1234567890123456789.0, 1234567890123456789.0)]
        [InlineData( 1.0000000000000000000, 1.0000000000000000000)]
        public void Number_IsOk(decimal value, decimal expected)
        {
            //Arrange
            var target = new NumberSetter();
            
            FhirResourceTypeId ResourceType = FhirResourceTypeId.Patient;
            ChargeItem resource = TestResourceFactory.ChargeItemResource.GetChargeItem();
            resource.FactorOverride = value;
            
            var typedElementList = GetTypedElementList(expression: "ChargeItem.factorOverride", resource: resource);

            Assert.Single(typedElementList);
            
            foreach (var typedElement in typedElementList)
            {
                //Act
                IList<IndexQuantity> indexList = target.Set(typedElement: typedElement, resourceType: ResourceType, searchParameterId: 1, searchParameterName: "the-search-parameter-code");
                
                //Assert
                Assert.Single(indexList);
                Assert.Equal(expected, indexList.First().Quantity);
            }
        }
        
        private IEnumerable<ITypedElement> GetTypedElementList(string expression,
            Resource resource)
        {
            ScopedNode resourceModel = new ScopedNode(resource.ToTypedElement());
                
            var FhirPathResolveMock = new Mock<IFhirPathResolve>();

            IEnumerable<ITypedElement> typedElementList = resourceModel.Select(
                expression: expression,
                ctx: new FhirEvaluationContext(resourceModel)
                {
                    ElementResolver = FhirPathResolveMock.Object.Resolver
                });
            return typedElementList;
        }
    }
}