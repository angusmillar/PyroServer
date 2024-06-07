using System;
using Microsoft.Extensions.Options;
using Moq;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Xunit;

namespace Abm.Pyro.Domain.Test.FhirSupport;


public class FhirUriFactoryTest
  {
    private const string BaseUrlServer = "http://base/stuff";
    private const string BaseUrlRemote = "http://remoteBase/stuff";


    private IFhirUriFactory GetFhirUriFactory(string serversBase)
    {
      var orderRepositorySettingsOptionsMock = new Mock<IOptions<ServiceBaseUrlSettings>>();
      orderRepositorySettingsOptionsMock.Setup(x => x.Value)
        .Returns(new ServiceBaseUrlSettings()
        {
          Url = new Uri(serversBase)
        });
      
      // Prepare
     return new FhirUriFactory(orderRepositorySettingsOptionsMock.Object, new FhirResourceTypeSupport());
    }
    
    
    [Fact]
    public void IsAbsoluteUriFalseTest()
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory("https://SomeFhirServer.com.au/over-here/fhir");

      string requestUri = "Patient/100";
      if (fhirUriFactory.TryParse(requestUri,out FhirUri? fhirUri, out string errorMessage))
      {
        Assert.False(fhirUri.IsAbsoluteUri);
        Assert.Equal("Patient",fhirUri.ResourceName);
        Assert.Equal("100",fhirUri.ResourceId);
        Assert.NotNull(fhirUri.UriPrimaryServiceRoot);
      }

      Assert.NotNull(fhirUri);
    }
    
    [Fact]
    public void IsAbsoluteUriTrueTest()
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory("https://SomeFhirServer.com.au/over-here/fhir");

      string requestUri = "https://OtherFhirServer.com.au/fhir/Patient/100";
      if (fhirUriFactory.TryParse(requestUri,out FhirUri? fhirUri, out string errorMessage))
      {
        Assert.True(fhirUri.IsAbsoluteUri);
        Assert.Equal("Patient",fhirUri.ResourceName);
        Assert.Equal("100",fhirUri.ResourceId);
        Assert.NotNull(fhirUri.UriPrimaryServiceRoot);
      }

      Assert.NotNull(fhirUri);
    }
    
    
    [Fact]
    public void ServiceBaseUriMatch()
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory("https://SomeFhirServer.com.au/over-here/fhir");

      string requestUri = "https://SomeFhirServer.com.au/over-here/fhir/Patient/100";
      if (fhirUriFactory.TryParse(requestUri,out FhirUri? fhirUri, out string errorMessage))
      {
        Assert.True(fhirUri.IsRelativeToServer);
        Assert.Null(fhirUri.PrimaryServiceRootRemote);
        Assert.NotNull(fhirUri.UriPrimaryServiceRoot);
      }

      Assert.NotNull(fhirUri);
    }
    
    [Fact]
    public void ConditionalDelete()
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory("https://SomeFhirServer.com.au/over-here/fhir");

      string requestUri = "Patient?identifier=123456";
      if (fhirUriFactory.TryParse(requestUri,out FhirUri? fhirUri, out string errorMessage))
      {
        Assert.Equal("Patient", fhirUri.ResourceName);
        Assert.Equal(string.Empty, fhirUri.ResourceId);
        Assert.True(fhirUri.IsRelativeToServer);
        
      }

      Assert.NotNull(fhirUri);
    }
    
    [Fact]
    public void ServiceBaseUriMatchHostCaseInsensitive()
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory("https://SomeFhirServer.com.au/over-here/fhir");

      string requestUri = "https://SOMEFHIRSERVER.cOm.aU/over-here/fhir/Patient/100";
      if (fhirUriFactory.TryParse(requestUri,out FhirUri? fhirUri, out string errorMessage))
      {
        Assert.True(fhirUri.IsRelativeToServer);
        Assert.Null(fhirUri.PrimaryServiceRootRemote);
        Assert.NotNull(fhirUri.UriPrimaryServiceRoot);
      }

      Assert.NotNull(fhirUri);
    }
    
    [Fact]
    public void ServiceBaseUriMissMatchPathCaseSensitive()
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory("https://SomeFhirServer.com.au/over-here/fhir");

      string requestUri = "https://SomeFhirServer.com.au/OVER-HERE/FHIR/Patient/100";
      if (fhirUriFactory.TryParse(requestUri,out FhirUri? fhirUri, out string errorMessage))
      {
        Assert.False(fhirUri.IsRelativeToServer);
        Assert.NotNull(fhirUri.PrimaryServiceRootRemote);
        Assert.NotNull(fhirUri.UriPrimaryServiceRoot);
      }

      Assert.NotNull(fhirUri);
    }
    
    [Fact]
    public void FhirUriFactoryIsRelativeToServerHostCaseInsensitiveTest()
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory("https://SomeFhirServer.com.au/over-here/fhir");

      string requestUri = "https://somefhirserver.com.au/over-here/fhir/Patient/100";
      if (fhirUriFactory.TryParse(requestUri,out FhirUri? fhirUri, out string errorMessage))
      {
        Assert.Equal("Patient", fhirUri.ResourceName);
        Assert.True(fhirUri.IsRelativeToServer);
      }

      Assert.NotNull(fhirUri);
    }
    
    [Fact]
    public void FhirUriFactoryNotRelativeToServerPathCaseSensitiveTest()
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory("https://127.0.0.1:777/Over-Here/fhir");

      string requestUri = "https://localhost:777/Over-Here/fhir/Patient/100";
      if (fhirUriFactory.TryParse(requestUri,out FhirUri? fhirUri, out string errorMessage))
      {
        Assert.Equal("Patient", fhirUri.ResourceName);
        Assert.False(fhirUri.IsRelativeToServer);
      }

      Assert.NotNull(fhirUri);
    }
    
    [Theory]
    [InlineData( BaseUrlServer, BaseUrlServer, "Patient", "10", "11")]
    [InlineData( BaseUrlServer, BaseUrlServer, "Patient", "10", "")]
    [InlineData( BaseUrlServer, BaseUrlServer, "Patient", "", "")]
    [InlineData( BaseUrlServer, BaseUrlRemote, "Patient", "10", "11")]
    [InlineData( BaseUrlServer, BaseUrlRemote, "Patient", "1132b5d1-10c6-4293-a0e3-7bccb1462e3a", "11")]
    [InlineData( BaseUrlServer, "", "Patient", "1132b5d1-10c6-4293-a0e3-7bccb1462e3a", "11")]
    public void TestFhirUriHistory(string serversBase, string requestBase, string resourceName, string resourceId, string versionId)
    {
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl;
      if (!string.IsNullOrWhiteSpace(versionId))
      {
        requestUrl = $"{resourceName}/{resourceId}/_history/{versionId}";
      }
      else
      {
        if (!string.IsNullOrWhiteSpace(resourceId))
        {
          requestUrl = $"{resourceName}/{resourceId}";
        }
        else
        {
          requestUrl = $"{resourceName}";
        }
      }
      if (!string.IsNullOrWhiteSpace(requestBase))
      {
        requestUrl = $"{requestBase}/{requestUrl}";
      }

      //Act
      if (fhirUriFactory.TryParse(requestUrl, out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        if (fhirUri is not null)
        {
          Assert.Equal(resourceName, fhirUri.ResourceName);

          if (!string.IsNullOrWhiteSpace(resourceId))
          {
            if (resourceId.StartsWith('#'))
            {
              Assert.True(fhirUri.IsContained);
              Assert.Equal(resourceId.TrimStart('#'), fhirUri.ResourceId);
            }
            else
            {
              Assert.False(fhirUri.IsContained);
              Assert.Equal(resourceId, fhirUri.ResourceId);
            }

          }
          else
          {
            Assert.Equal(string.Empty, fhirUri.ResourceId);
          }

          if (!string.IsNullOrWhiteSpace(versionId))
          {
            Assert.True(fhirUri.IsHistoryReference);
            Assert.Equal(versionId, fhirUri.VersionId);
          }
          else
          {
            Assert.False(fhirUri.IsHistoryReference);
            Assert.Equal(string.Empty, fhirUri.VersionId);
          }

          if (serversBase == requestBase)
          {
            Assert.Null(fhirUri.PrimaryServiceRootRemote);
            Assert.True(fhirUri.IsRelativeToServer);
            Assert.Equal(new Uri(requestBase), fhirUri.UriPrimaryServiceRoot);
          }
          else
          {
            if (!string.IsNullOrWhiteSpace(requestBase))
            {
              Assert.NotNull(fhirUri.PrimaryServiceRootRemote);
              Assert.False(fhirUri.IsRelativeToServer);
              Assert.Equal(new Uri(requestBase), fhirUri.PrimaryServiceRootRemote);
            }
            else
            {
              Assert.Null(fhirUri.PrimaryServiceRootRemote);
              Assert.True(fhirUri.IsRelativeToServer);
            }
          }

          Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsOperation);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.IsUrn);
          Assert.False(fhirUri.IsFormDataSearch);
          Assert.Equal(requestUrl, fhirUri.OriginalString);
          Assert.Equal(string.Empty, fhirUri.CompartmentalisedResourceName);
          Assert.Equal(string.Empty, fhirUri.OperationName);
          Assert.Null(fhirUri.OperationType);
          Assert.Equal(string.Empty, fhirUri.Query);
          Assert.Equal(string.Empty, fhirUri.Urn);
          Assert.Null(fhirUri.UrnType);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);
        }
      }
      else
      {
        Assert.Equal("some error message", errorMessage);
      }
    }

    [Theory]
    [InlineData( "Patient", "10", "11")]
    public void TestFhirUriCanonical(string resourceName, string resourceId, string canonicalVersionId)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(BaseUrlServer);


      string requestUrl = $"{resourceName}/{resourceId}|{canonicalVersionId}";

      //Act
      if (fhirUriFactory.TryParse(requestUrl, out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        if (fhirUri is not null)
        {
          Assert.Equal(resourceName, fhirUri.ResourceName);
          Assert.Equal(resourceId, fhirUri.ResourceId);
          Assert.Equal(string.Empty, fhirUri.VersionId);
          Assert.Equal(canonicalVersionId, fhirUri.CanonicalVersionId);

          Assert.Equal(new Uri(BaseUrlServer), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsOperation);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.IsUrn);
          Assert.False(fhirUri.IsFormDataSearch);
          Assert.Equal(requestUrl, fhirUri.OriginalString);
          Assert.Equal(string.Empty, fhirUri.CompartmentalisedResourceName);
          Assert.Equal(string.Empty, fhirUri.OperationName);
          Assert.Null(fhirUri.OperationType);
          Assert.Equal(string.Empty, fhirUri.Query);
          Assert.Equal(string.Empty, fhirUri.Urn);
          Assert.Null(fhirUri.UrnType);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);
        }
      }
      else
      {
        Assert.Equal("some error message", errorMessage);
      }
    }

    [Theory]
    [InlineData( BaseUrlServer, BaseUrlRemote)]
    public void TestFhirUriBaseHistory( string serversBase, string requestBase)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl = $"{requestBase}/_history";

      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        if (fhirUri is not null)
        {
          Assert.True(fhirUri.IsHistoryReference);
          Assert.Equal(string.Empty, fhirUri.ResourceName);
          Assert.False(fhirUri.IsContained);
          Assert.Equal(string.Empty, fhirUri.ResourceId);
          Assert.Equal(string.Empty, fhirUri.VersionId);
          Assert.Equal(new Uri(BaseUrlRemote), fhirUri.PrimaryServiceRootRemote);
          Assert.False(fhirUri.IsRelativeToServer);
          Assert.Equal(new Uri(requestBase), fhirUri.UriPrimaryServiceRoot);
          Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsOperation);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.IsUrn);
          Assert.False(fhirUri.IsFormDataSearch);
          Assert.Equal(requestUrl, fhirUri.OriginalString);
          Assert.Equal(string.Empty, fhirUri.CompartmentalisedResourceName);
          
          Assert.Equal(string.Empty, fhirUri.OperationName);
          Assert.Null(fhirUri.OperationType);
          Assert.Equal(string.Empty, fhirUri.Query);
          Assert.Equal(string.Empty, fhirUri.Urn);
          Assert.Null(fhirUri.UrnType);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);
        }
      }
      else
      {
        Assert.Equal("some error message", errorMessage);
      }
    }

    [Theory]
    [InlineData( BaseUrlServer, "10")]
    [InlineData( BaseUrlServer, "#10")]
    [InlineData( BaseUrlServer, "1132b5d1-10c6-4293-a0e3-7bccb1462e3a")]
    [InlineData( BaseUrlServer, "#1132b5d1-10c6-4293-a0e3-7bccb1462e3a")]
    public void TestFhirUri_ResourceIdOnly( string serversBase, string resourceId)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      //Act
      if (fhirUriFactory.TryParse(resourceId,  out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        if (fhirUri is not null)
        {

          if (resourceId.StartsWith('#'))
            Assert.True(fhirUri.IsContained);
          else
            Assert.False(fhirUri.IsContained);

          Assert.Equal(string.Empty, fhirUri.ResourceName);
          Assert.False(fhirUri.IsRelativeToServer);
          Assert.Equal(resourceId.TrimStart('#'), fhirUri.ResourceId);
          Assert.Null(fhirUri.UriPrimaryServiceRoot);
          Assert.False(fhirUri.IsHistoryReference);
          Assert.Equal(string.Empty, fhirUri.VersionId);
          Assert.Null(fhirUri.PrimaryServiceRootRemote);
          Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsOperation);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.IsUrn);
          Assert.False(fhirUri.IsFormDataSearch);
          Assert.Equal(resourceId, fhirUri.OriginalString);
          Assert.Equal(string.Empty, fhirUri.CompartmentalisedResourceName);
          
          Assert.Equal(string.Empty, fhirUri.OperationName);
          Assert.Null(fhirUri.OperationType);
          Assert.Equal(string.Empty, fhirUri.Query);
          Assert.Equal(string.Empty, fhirUri.Urn);
          Assert.Null(fhirUri.UrnType);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);
        }
      }
      else
      {
        Assert.Equal("some error message", errorMessage);
      }
    }

    [Fact]
    public void IsValidContained()
    {
      //Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(BaseUrlServer);

      string fhirUriString = "#100";
      //Act
      bool result = fhirUriFactory.TryParse(fhirUriString, out FhirUri? fhirUri, out string errorMessage);
      Assert.True(result);
      Assert.NotNull(fhirUri);
      Assert.Equal(string.Empty, fhirUri.ResourceName);
      Assert.Equal(fhirUriString.Trim('#'), fhirUri.ResourceId);
      Assert.True(fhirUri.IsContained);
      
    }
    
    [Fact]
    public void IsInValidContained()
    {
      //Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(BaseUrlServer);

      string fhirUriString = "Patient/#100";
      //Act
      bool result = fhirUriFactory.TryParse(fhirUriString, out FhirUri? fhirUri, out string errorMessage);
      Assert.False(result);
      Assert.Equal("A contained reference must not have a preceding resource name, however found: Patient", errorMessage);
      
    }
    
    [Theory]
    [InlineData( BaseUrlServer, "Patient", "10")]
    [InlineData( BaseUrlServer, "", "#10")]
    [InlineData( BaseUrlServer, "", "10")]
    public void TestFhirUri_Contained( string serversBase, string resourceName, string resourceId)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl;
      if (!string.IsNullOrWhiteSpace(resourceName))
      {
        requestUrl = $"{resourceName}/{resourceId}";
      }
      else
      {
        requestUrl = $"{resourceId}";
      }


      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        if (fhirUri is not null)
        {

          if (!string.IsNullOrWhiteSpace(resourceName))
          {
            Assert.Equal(resourceName, fhirUri.ResourceName);
          }
          else
          {
            Assert.Equal(string.Empty, fhirUri.ResourceName);
          }

          if (!string.IsNullOrWhiteSpace(resourceId))
          {
            if (resourceId.StartsWith('#'))
            {
              Assert.True(fhirUri.IsContained);
              //False as it is relative to the resource not the server
              Assert.False(fhirUri.IsRelativeToServer);
              Assert.Equal(resourceId.TrimStart('#'), fhirUri.ResourceId);
              Assert.Null(fhirUri.PrimaryServiceRootRemote);
            }
            else
            {
              Assert.False(fhirUri.IsContained);
              Assert.Equal(resourceId, fhirUri.ResourceId);
              if (string.IsNullOrWhiteSpace(resourceName))
              {
                Assert.False(fhirUri.IsRelativeToServer);
                Assert.Null(fhirUri.PrimaryServiceRootRemote);
              }
              else
              {
                Assert.True(fhirUri.IsRelativeToServer);
                Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
              }
            }
          }

          Assert.False(fhirUri.IsHistoryReference);
          Assert.Equal(string.Empty, fhirUri.VersionId);
          Assert.Null(fhirUri.PrimaryServiceRootRemote);


          Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsOperation);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.IsUrn);
          Assert.False(fhirUri.IsFormDataSearch);
          Assert.Equal(requestUrl, fhirUri.OriginalString);
          Assert.Equal(string.Empty, fhirUri.CompartmentalisedResourceName);
          
          Assert.Equal(string.Empty, fhirUri.OperationName);
          Assert.Null(fhirUri.OperationType);
          Assert.Equal(string.Empty, fhirUri.Query);
          Assert.Equal(string.Empty, fhirUri.Urn);
          Assert.Null(fhirUri.UrnType);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);
        }
      }
      else
      {
        Assert.Equal("some error message", errorMessage);
      }
    }
    
    [Theory]
    [InlineData( BaseUrlServer, BaseUrlServer, "MyOperation", "query1=one,query2=two")]
    public void TestFhirUri_OperationBase( string serversBase, string requestBase, string operationName, string query)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl = $"{requestBase}/${operationName}?{query}";

      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        if (fhirUri is not null)
        {
          Assert.Equal(operationName, fhirUri.OperationName);
          Assert.Equal(OperationScope.Base, fhirUri.OperationType);
          Assert.Equal(string.Empty, fhirUri.ResourceName);
          Assert.Equal(string.Empty, fhirUri.ResourceId);
          Assert.False(fhirUri.IsContained);
          Assert.True(fhirUri.IsRelativeToServer);
          Assert.Equal(new Uri(serversBase), fhirUri.UriPrimaryServiceRoot);
          Assert.False(fhirUri.IsHistoryReference);
          Assert.Equal(string.Empty, fhirUri.VersionId);
          Assert.Null(fhirUri.PrimaryServiceRootRemote);
          Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.IsUrn);
          Assert.False(fhirUri.IsFormDataSearch);
          Assert.Equal(requestUrl, fhirUri.OriginalString);
          Assert.Equal(string.Empty, fhirUri.CompartmentalisedResourceName);
          
          Assert.Equal(query, fhirUri.Query);
          Assert.Equal(string.Empty, fhirUri.Urn);
          Assert.Null(fhirUri.UrnType);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);
        }
      }
      else
      {
        Assert.Equal("some error message", errorMessage);
      }
    }

    [Theory]
    [InlineData( BaseUrlServer, BaseUrlServer, "Patient", "MyOperation", "query1=one,query2=two")]
    public void TestFhirUri_OperationResource( string serversBase, string requestBase, string resourceName, string operationName, string query)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl = $"{requestBase}/{resourceName}/${operationName}?{query}";

      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        if (fhirUri is not null)
        {
          Assert.Equal(operationName, fhirUri.OperationName);
          Assert.Equal(OperationScope.Resource, fhirUri.OperationType);
          Assert.Equal(resourceName, fhirUri.ResourceName);
          Assert.Equal(string.Empty, fhirUri.ResourceId);
          Assert.False(fhirUri.IsContained);
          Assert.True(fhirUri.IsRelativeToServer);
          Assert.Equal(new Uri(serversBase), fhirUri.UriPrimaryServiceRoot);
          Assert.False(fhirUri.IsHistoryReference);
          Assert.Equal(string.Empty, fhirUri.VersionId);
          Assert.Null(fhirUri.PrimaryServiceRootRemote);
          Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.IsUrn);
          Assert.False(fhirUri.IsFormDataSearch);
          Assert.Equal(requestUrl, fhirUri.OriginalString);
          Assert.Equal(string.Empty, fhirUri.CompartmentalisedResourceName);
          
          Assert.Equal(query, fhirUri.Query);
          Assert.Equal(string.Empty, fhirUri.Urn);
          Assert.Null(fhirUri.UrnType);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);
        }
      }
      else
      {
        Assert.Equal("some error message", errorMessage);
      }
    }

    [Theory]
    [InlineData( BaseUrlServer, BaseUrlServer, "Patient", "10", "MyOperation", "query1=one,query2=two")]
    public void TestFhirUri_OperationResourceInstance( string serversBase, string requestBase, string resourceName, string resourceId, string operationName, string query)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl = $"{requestBase}/{resourceName}/{resourceId}/${operationName}?{query}";

      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        if (fhirUri is not null)
        {
          Assert.Equal(operationName, fhirUri.OperationName);
          Assert.Equal(OperationScope.Instance, fhirUri.OperationType);
          Assert.Equal(resourceName, fhirUri.ResourceName);
          Assert.Equal(resourceId, fhirUri.ResourceId);
          Assert.False(fhirUri.IsContained);
          Assert.True(fhirUri.IsRelativeToServer);
          Assert.Equal(new Uri(serversBase), fhirUri.UriPrimaryServiceRoot);
          Assert.False(fhirUri.IsHistoryReference);
          Assert.Equal(string.Empty, fhirUri.VersionId);
          Assert.Null(fhirUri.PrimaryServiceRootRemote);
          Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.IsUrn);
          Assert.False(fhirUri.IsFormDataSearch);
          Assert.Equal(requestUrl, fhirUri.OriginalString);
          Assert.Equal(string.Empty, fhirUri.CompartmentalisedResourceName);
          
          Assert.Equal(query, fhirUri.Query);
          Assert.Equal(string.Empty, fhirUri.Urn);
          Assert.Null(fhirUri.UrnType);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);
        }
      }
      else
      {
        Assert.Equal("some error message", errorMessage);
      }
    }

    [Theory]
    [InlineData( BaseUrlServer, BaseUrlServer, "Patient", "_search")]
    public void TestFhirUri_FormData( string serversBase, string requestBase, string resourceName, string search)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl = $"{requestBase}/{resourceName}/{search}";

      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        if (fhirUri is not null)
        {
          Assert.Equal(string.Empty, fhirUri.OperationName);
          Assert.Null(fhirUri.OperationType);
          Assert.Equal(resourceName, fhirUri.ResourceName);
          Assert.Equal(string.Empty, fhirUri.ResourceId);
          Assert.False(fhirUri.IsContained);
          Assert.True(fhirUri.IsRelativeToServer);
          Assert.Equal(new Uri(serversBase), fhirUri.UriPrimaryServiceRoot);
          Assert.False(fhirUri.IsHistoryReference);
          Assert.Equal(string.Empty, fhirUri.VersionId);
          Assert.Null(fhirUri.PrimaryServiceRootRemote);
          Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.IsUrn);
          Assert.True(fhirUri.IsFormDataSearch);
          Assert.Equal(requestUrl, fhirUri.OriginalString);
          Assert.Equal(string.Empty, fhirUri.CompartmentalisedResourceName);
          
          Assert.Equal(string.Empty, fhirUri.Query);
          Assert.Equal(string.Empty, fhirUri.Urn);
          Assert.Null(fhirUri.UrnType);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);
        }
      }
      else
      {
        Assert.Equal("some error message", errorMessage);
      }
    }

    [Theory]
    [InlineData( BaseUrlServer, BaseUrlServer, "Patient")]
    public void TestFhirUri_RubbishOnTheEnd( string serversBase, string requestBase, string resourceName)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl = $"{requestBase}/{resourceName}/10/Rubbish";

      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        Assert.False(true);
      }
      else
      {
        Assert.True(!string.IsNullOrWhiteSpace(errorMessage));
        Assert.Null(fhirUri);
      }

    }

    [Theory]
    [InlineData( BaseUrlServer, "urn:uuid:61ebe359-bfdc-4613-8bf2-c5e300945f0a")]
    public void TestFhirUri_urn_uuid( string serversBase, string uuid)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl = uuid;

      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        if (fhirUri is not null)
        {
          Assert.Equal(string.Empty, fhirUri.OperationName);
          Assert.Null(fhirUri.OperationType);
          Assert.Equal(string.Empty, fhirUri.ResourceName);
          Assert.Equal(string.Empty, fhirUri.ResourceId);
          Assert.False(fhirUri.IsContained);
          Assert.False(fhirUri.IsRelativeToServer);
          Assert.Null(fhirUri.PrimaryServiceRootRemote);
          Assert.False(fhirUri.IsHistoryReference);
          Assert.Equal(string.Empty, fhirUri.VersionId);
          Assert.Null(fhirUri.PrimaryServiceRootRemote);
          Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.IsFormDataSearch);
          Assert.Equal(requestUrl, fhirUri.OriginalString);
          Assert.Equal(string.Empty, fhirUri.CompartmentalisedResourceName);
          
          Assert.Equal(string.Empty, fhirUri.Query);
          Assert.Equal(UrnType.uuid, fhirUri.UrnType);
          Assert.Equal(uuid.Substring("urn:uuid:".Length), fhirUri.Urn);
          Assert.True(fhirUri.IsUrn);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);
        }
      }
      else
      {
        Assert.Equal("some error message", errorMessage);
      }
    }

    [Theory]
    [InlineData( BaseUrlServer, "urn:oid:1.2.36.1.2001.1001.101")]
    public void TestFhirUri_urn_oid( string serversBase, string uuid)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl = uuid;

      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        if (fhirUri is not null)
        {
          Assert.Equal(string.Empty, fhirUri.OperationName);
          Assert.Null(fhirUri.OperationType);
          Assert.Equal(string.Empty, fhirUri.ResourceName);
          Assert.Equal(string.Empty, fhirUri.ResourceId);
          Assert.False(fhirUri.IsContained);
          Assert.False(fhirUri.IsRelativeToServer);
          Assert.Null(fhirUri.PrimaryServiceRootRemote);
          Assert.False(fhirUri.IsHistoryReference);
          Assert.Equal(string.Empty, fhirUri.VersionId);
          Assert.Null(fhirUri.PrimaryServiceRootRemote);
          Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsCompartment);
          Assert.False(fhirUri.IsFormDataSearch);
          Assert.Equal(requestUrl, fhirUri.OriginalString);
          Assert.Equal(string.Empty, fhirUri.CompartmentalisedResourceName);
          
          Assert.Equal(string.Empty, fhirUri.Query);
          Assert.Equal(UrnType.oid, fhirUri.UrnType);
          Assert.Equal(uuid.Substring("urn:oid:".Length), fhirUri.Urn);
          Assert.True(fhirUri.IsUrn);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);
        }
      }
      else
      {
        Assert.Equal("some error message", errorMessage);
      }
    }

    [Theory]
    [InlineData( BaseUrlServer, "urn:oid:1.2.36.1.ABC.2001.1001.101")]
    public void TestFhirUri__urn_oid_invalid( string serversBase, string uuid)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl = uuid;

      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        Assert.False(true);
      }
      else
      {
        Assert.True(!string.IsNullOrWhiteSpace(errorMessage));
        Assert.Null(fhirUri);
      }


    }

    [Theory]
    [InlineData( BaseUrlServer, "urn:uuid:61ebe359-XXXX-4613-8bf2-c5e300945f0a")]
    public void TestFhirUri__urn_uuid_invalid( string serversBase, string uuid)
    {
      // Prepare
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl = uuid;

      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        Assert.False(true);
      }
      else
      {
        Assert.True(!string.IsNullOrWhiteSpace(errorMessage));
        Assert.Null(fhirUri);
      }

    }

    [Theory]
    [InlineData( BaseUrlServer, BaseUrlServer, "Patient", "10", "Encounter", "")]
    //Incorrect Compartment as it is not a known ResourceType
    [InlineData( BaseUrlServer, BaseUrlServer, "Patient", "10", "Unknown", "")]
    [InlineData( BaseUrlServer, BaseUrlServer, "Patient", "10", "Unknown", "query_one=one, query_two=two")]
    public void TestFhirUri_Compartment( string serversBase, string requestBase, string resourceName, string resourceId, string compartmentName, string query)
    {
      // Prepare
      //hit: the resource is unknown because we do not pass it into the GetFhirUriFactory below, we only pass in the resourceName and not the compatmentName 
      IFhirUriFactory fhirUriFactory = GetFhirUriFactory(serversBase);

      string requestUrl;
      if (string.IsNullOrWhiteSpace(query))
      {
        requestUrl = $"{requestBase}/{resourceName}/{resourceId}/{compartmentName}";
      }
      else
      {
        requestUrl = $"{requestBase}/{resourceName}/{resourceId}/{compartmentName}?{query}";
      }


      //Act
      if (fhirUriFactory.TryParse(requestUrl,  out FhirUri? fhirUri, out string errorMessage))
      {
        //Assert
        
          Assert.Equal(string.Empty, fhirUri.OperationName);
          Assert.Null(fhirUri.OperationType);
          Assert.Equal(resourceName, fhirUri.ResourceName);
          Assert.Equal(resourceId, fhirUri.ResourceId);
          Assert.False(fhirUri.IsContained);
          Assert.True(fhirUri.IsRelativeToServer);
          Assert.Equal(new Uri(serversBase), fhirUri.PrimaryServiceRootServers);
          Assert.False(fhirUri.IsHistoryReference);
          Assert.Equal(string.Empty, fhirUri.VersionId);
          Assert.Null(fhirUri.PrimaryServiceRootRemote);
          Assert.Equal(new Uri(serversBase), fhirUri.UriPrimaryServiceRoot);
          Assert.True(fhirUri.IsCompartment);
          Assert.Equal(compartmentName, fhirUri.CompartmentalisedResourceName);
          Assert.False(fhirUri.ErrorInParsing);
          Assert.False(fhirUri.IsMetaData);
          Assert.False(fhirUri.IsUrn);
          Assert.False(fhirUri.IsFormDataSearch);
          Assert.Equal(requestUrl, fhirUri.OriginalString);
          Assert.Equal(compartmentName, fhirUri.CompartmentalisedResourceName);
          Assert.True(fhirUri.IsCompartment);
          

          Assert.Equal(string.Empty, fhirUri.Urn);
          Assert.Null(fhirUri.UrnType);
          Assert.Equal(string.Empty, fhirUri.ParseErrorMessage);

          if (string.IsNullOrWhiteSpace(query))
            Assert.Equal(string.Empty, fhirUri.Query);
          else
            Assert.Equal(query, fhirUri.Query);
      }
      else
      {
        if (string.IsNullOrWhiteSpace(query))
          Assert.Equal("The URI has extra unknown content near the end of : 'Unknown'. The full URI was: 'http://base/stuff/Patient/10/Unknown'", errorMessage);
        else
          Assert.Equal("The URI has extra unknown content near the end of : 'Unknown'. The full URI was: 'http://base/stuff/Patient/10/Unknown?query_one=one, query_two=two'", errorMessage);
      }
    }


  }