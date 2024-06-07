using System.Net;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.FhirSupport;
using Xunit;

namespace Abm.Pyro.Domain.Test.FhirSupport;

public class FhirResourceTypeSupportTest
{
    //Setup
    protected FhirResourceTypeSupportTest()
    {
    }

    public class IsResourceTypeString : FhirResourceTypeSupportTest
    {
        [Fact]
        public void True()
        {
            //Arrange
            var target = new FhirResourceTypeSupport();
            
            string resourceType = FhirResourceTypeId.Practitioner.GetCode();

            //Act
            bool isResourceTypeString = target.IsResourceTypeString(resourceType);

            //Assert
            Assert.True(isResourceTypeString);
        }
        
        [Fact]
        public void False()
        {
            //Arrange
            var target = new FhirResourceTypeSupport();
            
            string resourceType = "NotAResourceType";

            //Act
            bool isResourceTypeString = target.IsResourceTypeString(resourceType);

            //Assert
            Assert.False(isResourceTypeString);
        }
    }

    public class GetRequiredFhirResourceType : FhirResourceTypeSupportTest
    {
        [Fact]
        public void IsFhirResourceType()
        {
            //Arrange
            var target = new FhirResourceTypeSupport();
            
            string resourceType = FhirResourceTypeId.Practitioner.GetCode();

            //Act
            FhirResourceTypeId result = target.GetRequiredFhirResourceType(resourceType);

            //Assert
            Assert.Equal(FhirResourceTypeId.Practitioner, result);
        }
        
        [Fact]
        public void IsNotFhirResourceType_Throws()
        {
            //Arrange
            var target = new FhirResourceTypeSupport();
            
            string resourceType = "NotAResourceType";

            //Act
            var exception = Assert.Throws<FhirFatalException>(() => target.GetRequiredFhirResourceType(resourceType));

            //Assert
            Assert.Equal(HttpStatusCode.InternalServerError, exception.HttpStatusCode);
        }
    }
    
    public class TryGetResourceType : FhirResourceTypeSupportTest
    {
        [Fact]
        public void IsFhirResourceType()
        {
            //Arrange
            var target = new FhirResourceTypeSupport();
            
            string resourceType = FhirResourceTypeId.Practitioner.GetCode();

            //Act
            FhirResourceTypeId? result = target.TryGetResourceType(resourceType);

            //Assert
            Assert.Equal(FhirResourceTypeId.Practitioner, result);
        }
        
        [Fact]
        public void IsNotFhirResourceType()
        {
            //Arrange
            var target = new FhirResourceTypeSupport();
            
            string resourceType = "NotAResourceType";

            //Act
            FhirResourceTypeId? result = target.TryGetResourceType(resourceType);

            //Assert
            Assert.Null(result);
        }
    }
}