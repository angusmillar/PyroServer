using System;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Support;
using Xunit;

namespace Abm.Pyro.Domain.Test.FhirSupport;

public class FhirResponseHttpHeaderSupportTest
{
    //Setup
    protected FhirResponseHttpHeaderSupportTest()
    {
    }

    public class AddXRequestId : FhirResponseHttpHeaderSupportTest
    {
        [Fact]
        public void HeaderDictionary_HasXRequestId()
        {
            //Arrange
            var target = new FhirResponseHttpHeaderSupport();

            var headers = new Dictionary<string, StringValues>();
            string requestId = "my-request-id";

            //Act
            target.AddXRequestId(responseHeaders: headers, xRequestId: requestId);

            //Assert
            Assert.Equal(requestId, headers[HttpHeaderName.XRequestId]);
            Assert.Single(headers);
        }
    }

    public class AddXCorrelationId : FhirResponseHttpHeaderSupportTest
    {
        [Fact]
        public void HeaderDictionary_HasXCorrelationId()
        {
            //Arrange
            var target = new FhirResponseHttpHeaderSupport();

            var headers = new Dictionary<string, StringValues>();
            string correlationId = "my-correlation-id";

            //Act
            target.AddXCorrelationId(responseHeaders: headers, xCorrelationId: correlationId);

            //Assert
            Assert.Equal(correlationId, headers[HttpHeaderName.XCorrelationId]);
            Assert.Single(headers);
        }
    }

    public class ForCreate : FhirResponseHttpHeaderSupportTest
    {
        [Fact]
        public void AreCorrect()
        {
            //Arrange
            var target = new FhirResponseHttpHeaderSupport();

            FhirResourceTypeId resourceType = FhirResourceTypeId.Account;
            DateTime resourceLastUpdatedDateTimeUtc = DateTime.Now;
            string resourceId = "resource-id";
            int versionId = 5;
            DateTimeOffset requestTimeStamp = DateTimeOffset.Now;
            string requestSchema = "https";
            string serviceBaseUrl = "thisFhirServer.com.au/fhir";

            //Act
            Dictionary<string, StringValues> headers = target.ForCreate(
                resourceType: resourceType,
                lastUpdatedUtc: resourceLastUpdatedDateTimeUtc,
                resourceId: resourceId,
                versionId: versionId,
                requestTimeStamp: requestTimeStamp,
                requestSchema: requestSchema,
                serviceBaseUrl: serviceBaseUrl);

            //Assert
            Assert.Equal(requestTimeStamp.ToString("r"), headers[HttpHeaderName.Date]);
            Assert.Equal(resourceLastUpdatedDateTimeUtc.ToString("r"), headers[HttpHeaderName.LastModified]);
            Assert.Equal(StringSupport.GetEtag(versionId), headers[HttpHeaderName.ETag]);
            Assert.Equal($"{requestSchema}://{serviceBaseUrl}/{resourceType.ToString()}/{resourceId}/_history/{versionId.ToString()}", headers[HttpHeaderName.Location]);
            Assert.Equal(4, headers.Count);
        }
    }

    public class ForUpdate : FhirResponseHttpHeaderSupportTest
    {
        [Fact]
        public void WhereVersionNotEqualToOne_AreCorrect()
        {
            //Arrange
            var target = new FhirResponseHttpHeaderSupport();
            
            DateTime resourceLastUpdatedDateTimeUtc = DateTime.Now;
            int versionId = 5;
            DateTimeOffset requestTimeStamp = DateTimeOffset.Now;

            //Act
            Dictionary<string, StringValues> headers = target.ForUpdate(
                lastUpdatedUtc: resourceLastUpdatedDateTimeUtc,
                versionId: versionId,
                requestTimeStamp: requestTimeStamp);

            //Assert
            Assert.Equal(requestTimeStamp.ToString("r"), headers[HttpHeaderName.Date]);
            Assert.Equal(resourceLastUpdatedDateTimeUtc.ToString("r"), headers[HttpHeaderName.LastModified]);
            Assert.Equal(StringSupport.GetEtag(versionId), headers[HttpHeaderName.ETag]);
            Assert.False(headers.ContainsKey(HttpHeaderName.Location));
            Assert.Equal(3, headers.Count);
        }

        [Fact]
        public void WhereVersionOne_AreCorrect()
        {
            //Arrange
            var target = new FhirResponseHttpHeaderSupport();
            
            DateTime resourceLastUpdatedDateTimeUtc = DateTime.Now;
            int versionId = 1;
            DateTimeOffset requestTimeStamp = DateTimeOffset.Now;

            //Act
            Dictionary<string, StringValues> headers = target.ForUpdate(
                lastUpdatedUtc: resourceLastUpdatedDateTimeUtc,
                versionId: versionId,
                requestTimeStamp: requestTimeStamp);

            //Assert
            Assert.Equal(requestTimeStamp.ToString("r"), headers[HttpHeaderName.Date]);
            Assert.Equal(resourceLastUpdatedDateTimeUtc.ToString("r"), headers[HttpHeaderName.LastModified]);
            Assert.Equal(StringSupport.GetEtag(versionId), headers[HttpHeaderName.ETag]);
            Assert.Equal(3, headers.Count);
        }
    }

    public class ForRead : FhirResponseHttpHeaderSupportTest
    {
        [Fact]
        public void AreCorrect()
        {
            //Arrange
            var target = new FhirResponseHttpHeaderSupport();

            DateTime resourceLastUpdatedDateTimeUtc = DateTime.Now;
            int versionId = 5;
            DateTimeOffset requestTimeStamp = DateTimeOffset.Now;

            //Act
            Dictionary<string, StringValues> headers = target.ForRead(
                lastUpdatedUtc: resourceLastUpdatedDateTimeUtc,
                versionId: versionId,
                requestTimeStamp: requestTimeStamp);

            //Assert
            Assert.Equal(requestTimeStamp.ToString("r"), headers[HttpHeaderName.Date]);
            Assert.Equal(resourceLastUpdatedDateTimeUtc.ToString("r"), headers[HttpHeaderName.LastModified]);
            Assert.Equal(StringSupport.GetEtag(versionId), headers[HttpHeaderName.ETag]);
            Assert.Equal(3, headers.Count);
        }
    }

    public class ForDelete : FhirResponseHttpHeaderSupportTest
    {
        [Fact]
        public void HasVersionId_AreCorrect()
        {
            //Arrange
            var target = new FhirResponseHttpHeaderSupport();

            int versionId = 5;
            DateTimeOffset requestTimeStamp = DateTimeOffset.Now;

            //Act
            Dictionary<string, StringValues> headers = target.ForDelete(
                requestTimeStamp: requestTimeStamp,
                versionId: versionId);

            //Assert
            Assert.Equal(requestTimeStamp.ToString("r"), headers[HttpHeaderName.Date]);
            Assert.Equal(StringSupport.GetEtag(versionId), headers[HttpHeaderName.ETag]);
            Assert.Equal(2, headers.Count);
        }
        
        [Fact]
        public void NoVersionId_AreCorrect()
        {
            //Arrange
            var target = new FhirResponseHttpHeaderSupport();

            int? versionId = null;
            DateTimeOffset requestTimeStamp = DateTimeOffset.Now;

            //Act
            Dictionary<string, StringValues> headers = target.ForDelete(
                requestTimeStamp: requestTimeStamp,
                versionId: versionId);

            //Assert
            Assert.Equal(requestTimeStamp.ToString("r"), headers[HttpHeaderName.Date]);
            Assert.False(headers.ContainsKey(HttpHeaderName.ETag));
            Assert.Single(headers);
        }
    }
    
    public class ForSearch : FhirResponseHttpHeaderSupportTest
    {
        [Fact]
        public void AreCorrect()
        {
            //Arrange
            var target = new FhirResponseHttpHeaderSupport();
            
            DateTimeOffset requestTimeStamp = DateTimeOffset.Now;

            //Act
            Dictionary<string, StringValues> headers = target.ForSearch(
                requestTimeStamp: requestTimeStamp);

            //Assert
            Assert.Equal(requestTimeStamp.ToString("r"), headers[HttpHeaderName.Date]);
            Assert.Single(headers);
        }
    }
}