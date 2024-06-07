// using System.Collections.Generic;
// using System.Linq;
// using System.Net;
// using System.Threading;
// using Hl7.Fhir.Model;
// using Microsoft.Extensions.Primitives;
// using Moq;
// using Abm.Pyro.Application.FhirBundleService;
// using Abm.Pyro.Application.FhirHandler;
// using Abm.Pyro.Application.FhirResponse;
// using Abm.Pyro.Domain.FhirSupport;
// using Abm.Pyro.Domain.Support;
// using Abm.Pyro.Domain.Test.Factories;
// using Xunit;
// using Task = System.Threading.Tasks.Task;
//
// namespace Abm.Pyro.Application.Test.FhirBundleService;
//
// public class FhirTransactionPostServiceTest
// {
//     
//     private readonly Mock<IFhirBundleCommonSupport> FhirBundleCommonSupportMock;
//     private readonly Mock<IFhirCreateHandler> FhirCreateHandlerMock;
//     private readonly FhirRequestHttpHeaderSupport FhirRequestHttpHeaderSupport;
//     
//
//     private readonly Patient PatientResource;
//    
//     //Setup
//     protected FhirTransactionPostServiceTest()
//     {
//         FhirBundleCommonSupportMock = new Mock<IFhirBundleCommonSupport>();
//
//         PatientResource = TestResourceFactory.PatientResource.GetDonaldDuck();
//         FhirOptionalResourceResponse fhirResponse = new FhirOptionalResourceResponse(
//             Resource: PatientResource,
//             HttpStatusCode: HttpStatusCode.Created,
//             Headers: new Dictionary<string, StringValues>()
//             {
//                 { HttpHeaderName.ETag, new StringValues(StringSupport.GetEtag(1)) }
//             });
//         
//         FhirCreateHandlerMock = new Mock<IFhirCreateHandler>();
//         FhirCreateHandlerMock.Setup(x =>
//                 x.Handle(
//                     It.IsAny<string>(), 
//                     It.IsAny<Resource>(), 
//                     It.IsAny<Dictionary<string,StringValues>>(), 
//                     It.IsAny<CancellationToken>()))
//             .ReturnsAsync(fhirResponse);
//
//         FhirRequestHttpHeaderSupport = new FhirRequestHttpHeaderSupport();
//
//     }
//
//     public class ProcessPosts : FhirTransactionPostServiceTest
//     {
//         [Fact]
//         public async Task FullUrl_Is_urn_uuid()
//         {
//             string serviceBaseUrl = "https://service-base-url.com/fhir";
//             FhirBundleCommonSupport fhirBundleCommonSupport = new FhirBundleCommonSupport(FhirUriFactoryFactory.GetFhirUriFactory(serviceBaseUrl));
//             
//             //Arrange
//             var target = new FhirTransactionPostService(
//                 fhirBundleCommonSupport,
//                 FhirCreateHandlerMock.Object,
//                 FhirRequestHttpHeaderSupport);
//
//             Patient postPatientResource = TestResourceFactory.PatientResource.GetDonaldDuck();
//             postPatientResource.Id = null;
//             postPatientResource.Meta = null;
//             string fullUrl = $"urn:uuid:{GuidSupport.NewFhirGuid()}"; //Is urn_uuid
//             var entryList = new List<Bundle.EntryComponent>()
//             {
//                 new Bundle.EntryComponent()
//                 {
//                     FullUrl = fullUrl,
//                     Resource = postPatientResource,
//                     Request = new Bundle.RequestComponent()
//                     {
//                         Method = Bundle.HTTPVerb.POST, 
//                         Url = $"Patient"
//                     }
//                 }
//             };
//
//             var requestHeaders = new Dictionary<string, StringValues>();
//             
//             var cancellationTokenSource = new CancellationTokenSource();
//             
//             //Act
//             List<ResourceUpdateInfo> resourceUpdateInfoList = 
//                 await target.ProcessPosts(entryList, requestHeaders, cancellationTokenSource);
//             
//             //Verify
//             FhirCreateHandlerMock.Verify(x =>
//                     x.Handle(
//                         It.IsAny<string>(), 
//                         It.IsAny<Resource>(), 
//                         It.IsAny<Dictionary<string,StringValues>>(), 
//                         It.IsAny<CancellationToken>())
//                 , times: Times.Once);
//             
//             //Assert
//             Assert.Equal(StringSupport.GetEtag(1), entryList.First().Response.Etag);
//             Assert.Equal(HttpStatusCode.Created.Display(), entryList.First().Response.Status);
//             //Assert.Equal(patientResource, entryList.First().Resource);
//             Assert.StartsWith($"{serviceBaseUrl}/{PatientResource.TypeName}/", entryList.First().FullUrl);
//             Assert.Equal(fullUrl,resourceUpdateInfoList.First().fullUrl.OriginalString);
//             Assert.NotEqual(PatientResource.Id,resourceUpdateInfoList.First().NewResourceId);
//             Assert.NotEqual(PatientResource.Id,resourceUpdateInfoList.First().NewVersionId);
//         }
//         
//         [Fact]
//         public async Task FullUrl_Is_ForeignAbsoluteUrl()
//         {
//             string serviceBaseUrl = "https://service-base-url.com/fhir";
//             FhirBundleCommonSupport fhirBundleCommonSupport = new FhirBundleCommonSupport(FhirUriFactoryFactory.GetFhirUriFactory(serviceBaseUrl));
//             
//             //Arrange
//             var target = new FhirTransactionPostService(
//                 fhirBundleCommonSupport,
//                 FhirCreateHandlerMock.Object,
//                 FhirRequestHttpHeaderSupport);
//
//             Patient postPatientResource = TestResourceFactory.PatientResource.GetDonaldDuck();
//             string resourceId = GuidSupport.NewFhirGuid();
//             postPatientResource.Id = resourceId;
//             postPatientResource.Meta = null;
//             string fullUrl = $"https://some-other-service-base-url.com/fhir/Patient/{postPatientResource.Id}"; //Is ForeignAbsoluteUrl
//             var entryList = new List<Bundle.EntryComponent>()
//             {
//                 new Bundle.EntryComponent()
//                 {
//                     FullUrl = fullUrl,
//                     Resource = postPatientResource,
//                     Request = new Bundle.RequestComponent()
//                     {
//                         Method = Bundle.HTTPVerb.POST, 
//                         Url = $"Patient"
//                     }
//                 }
//             };
//
//             var requestHeaders = new Dictionary<string, StringValues>();
//             
//             var cancellationTokenSource = new CancellationTokenSource();
//             
//             //Act
//             List<ResourceUpdateInfo> resourceUpdateInfoList = 
//                 await target.ProcessPosts(entryList, requestHeaders, cancellationTokenSource);
//             
//             //Verify
//             FhirCreateHandlerMock.Verify(x =>
//                     x.Handle(
//                         It.IsAny<string>(), 
//                         It.IsAny<Resource>(), 
//                         It.IsAny<Dictionary<string,StringValues>>(), 
//                         It.IsAny<CancellationToken>())
//                 , times: Times.Once);
//             
//             //Assert
//             Assert.Equal(StringSupport.GetEtag(1), entryList.First().Response.Etag);
//             Assert.Equal(HttpStatusCode.Created.Display(), entryList.First().Response.Status);
//             Assert.NotNull(entryList.First().Resource);
//             Assert.StartsWith($"{serviceBaseUrl}/{postPatientResource.TypeName}/", entryList.First().FullUrl);
//             Assert.Equal(fullUrl,resourceUpdateInfoList.First().fullUrl.OriginalString);
//             Assert.NotEqual(resourceId,resourceUpdateInfoList.First().NewResourceId);
//             Assert.NotEqual(resourceId,resourceUpdateInfoList.First().NewResourceId);
//             
//         }
//         
//         [Fact]
//         public async Task FullUrl_Is_LocalAbsoluteUrl()
//         {
//             string serviceBaseUrl = "https://service-base-url.com/fhir";
//             FhirBundleCommonSupport fhirBundleCommonSupport = new FhirBundleCommonSupport(FhirUriFactoryFactory.GetFhirUriFactory(serviceBaseUrl));
//             
//             //Arrange
//             var target = new FhirTransactionPostService(
//                 fhirBundleCommonSupport,
//                 FhirCreateHandlerMock.Object,
//                 FhirRequestHttpHeaderSupport);
//
//             Patient postPatientResource = TestResourceFactory.PatientResource.GetDonaldDuck();
//             string resourceId = GuidSupport.NewFhirGuid();
//             postPatientResource.Id = resourceId;
//             postPatientResource.Meta = null;
//             string fullUrl = $"{serviceBaseUrl}/Patient/{postPatientResource.Id}"; //Is LocalAbsoluteUrl
//             var entryList = new List<Bundle.EntryComponent>()
//             {
//                 new Bundle.EntryComponent()
//                 {
//                     FullUrl = fullUrl,
//                     Resource = postPatientResource,
//                     Request = new Bundle.RequestComponent()
//                     {
//                         Method = Bundle.HTTPVerb.POST, 
//                         Url = $"Patient"
//                     }
//                 }
//             };
//
//             var requestHeaders = new Dictionary<string, StringValues>();
//             
//             var cancellationTokenSource = new CancellationTokenSource();
//             
//             //Act
//             List<ResourceUpdateInfo> resourceUpdateInfoList = 
//                 await target.ProcessPosts(entryList, requestHeaders, cancellationTokenSource);
//             
//             //Verify
//             FhirCreateHandlerMock.Verify(x =>
//                     x.Handle(
//                         It.IsAny<string>(), 
//                         It.IsAny<Resource>(), 
//                         It.IsAny<Dictionary<string,StringValues>>(), 
//                         It.IsAny<CancellationToken>())
//                 , times: Times.Once);
//             
//             //Assert
//             Assert.Equal(StringSupport.GetEtag(1), entryList.First().Response.Etag);
//             Assert.Equal(HttpStatusCode.Created.Display(), entryList.First().Response.Status);
//             Assert.NotNull(entryList.First().Resource);
//             Assert.StartsWith($"{serviceBaseUrl}/{postPatientResource.TypeName}/", entryList.First().FullUrl);
//             Assert.NotEqual(resourceId,resourceUpdateInfoList.First().NewResourceId);
//             Assert.NotEqual(resourceId,resourceUpdateInfoList.First().NewResourceId);
//             
//         }
//         
//         [Fact]
//         public async Task FullUrl_Is_RelativeUrl()
//         {
//             string serviceBaseUrl = "https://service-base-url.com/fhir";
//             FhirBundleCommonSupport fhirBundleCommonSupport = new FhirBundleCommonSupport(FhirUriFactoryFactory.GetFhirUriFactory(serviceBaseUrl));
//             
//             //Arrange
//             var target = new FhirTransactionPostService(
//                 fhirBundleCommonSupport,
//                 FhirCreateHandlerMock.Object,
//                 FhirRequestHttpHeaderSupport);
//
//             Patient postPatientResource = TestResourceFactory.PatientResource.GetDonaldDuck();
//             string resourceId = GuidSupport.NewFhirGuid();
//             postPatientResource.Id = resourceId;
//             postPatientResource.Meta = null;
//             string fullUrl = $"Patient/{postPatientResource.Id}"; //Is RelativeUrl
//             var entryList = new List<Bundle.EntryComponent>()
//             {
//                 new Bundle.EntryComponent()
//                 {
//                     FullUrl = fullUrl,
//                     Resource = postPatientResource,
//                     Request = new Bundle.RequestComponent()
//                     {
//                         Method = Bundle.HTTPVerb.POST, 
//                         Url = $"Patient"
//                     }
//                 }
//             };
//
//             var requestHeaders = new Dictionary<string, StringValues>();
//             
//             var cancellationTokenSource = new CancellationTokenSource();
//             
//             //Act
//             List<ResourceUpdateInfo> resourceUpdateInfoList = 
//                 await target.ProcessPosts(entryList, requestHeaders, cancellationTokenSource);
//             
//             
//             //Verify
//             FhirCreateHandlerMock.Verify(x =>
//                     x.Handle(
//                         It.IsAny<string>(), 
//                         It.IsAny<Resource>(), 
//                         It.IsAny<Dictionary<string,StringValues>>(), 
//                         It.IsAny<CancellationToken>())
//                 , times: Times.Once);
//             
//             //Assert
//             Assert.Equal(StringSupport.GetEtag(1), entryList.First().Response.Etag);
//             Assert.Equal(HttpStatusCode.Created.Display(), entryList.First().Response.Status);
//             Assert.NotNull(entryList.First().Resource);
//             Assert.StartsWith($"{serviceBaseUrl}/{postPatientResource.TypeName}/", entryList.First().FullUrl);
//             Assert.NotEqual(resourceId,resourceUpdateInfoList.First().NewResourceId);
//             Assert.NotEqual(resourceId,resourceUpdateInfoList.First().NewResourceId);
//             
//         }
//         
//         [Fact]
//         public async Task Conditional_Create()
//         {
//             string serviceBaseUrl = "https://service-base-url.com/fhir";
//             FhirBundleCommonSupport fhirBundleCommonSupport = new FhirBundleCommonSupport(FhirUriFactoryFactory.GetFhirUriFactory(serviceBaseUrl));
//             
//             //Arrange
//             var target = new FhirTransactionPostService(
//                 fhirBundleCommonSupport,
//                 FhirCreateHandlerMock.Object,
//                 FhirRequestHttpHeaderSupport);
//
//             Patient postPatientResource = TestResourceFactory.PatientResource.GetDonaldDuck();
//             string resourceId = GuidSupport.NewFhirGuid();
//             string ifNoneExistQueryString = "family=smith&given=john";
//             postPatientResource.Id = resourceId;
//             postPatientResource.Meta = null;
//             string fullUrl = $"Patient/{postPatientResource.Id}"; //Is RelativeUrl
//             var entryList = new List<Bundle.EntryComponent>()
//             {
//                 new Bundle.EntryComponent()
//                 {
//                     FullUrl = fullUrl,
//                     Resource = postPatientResource,
//                     Request = new Bundle.RequestComponent()
//                     {
//                         Method = Bundle.HTTPVerb.POST, 
//                         Url = $"{postPatientResource.TypeName}",
//                         IfNoneExist = ifNoneExistQueryString
//                     }
//                 }
//             };
//
//             var requestHeaders = new Dictionary<string, StringValues>();
//             
//             var cancellationTokenSource = new CancellationTokenSource();
//             
//             //Act
//             List<ResourceUpdateInfo> resourceUpdateInfoList = 
//                 await target.ProcessPosts(entryList, requestHeaders, cancellationTokenSource);
//             
//             //Verify
//             FhirCreateHandlerMock.Verify(x =>
//                     x.Handle(
//                         It.IsAny<string>(), 
//                         It.IsAny<Resource>(), 
//                         It.Is<Dictionary<string,StringValues>>(x => 
//                             x.ContainsKey(HttpHeaderName.IfNoneExist) && x[HttpHeaderName.IfNoneExist].Equals(ifNoneExistQueryString)), 
//                         It.IsAny<CancellationToken>())
//                 , times: Times.Once);
//             
//             //Assert
//             Assert.Equal(StringSupport.GetEtag(1), entryList.First().Response.Etag);
//             Assert.Equal(HttpStatusCode.Created.Display(), entryList.First().Response.Status);
//             Assert.NotNull(entryList.First().Resource);
//             Assert.StartsWith($"{serviceBaseUrl}/{postPatientResource.TypeName}/", entryList.First().FullUrl);
//             Assert.NotEqual(resourceId,resourceUpdateInfoList.First().NewResourceId);
//             Assert.NotEqual(resourceId,resourceUpdateInfoList.First().NewResourceId);
//             
//         }
//     }
// }