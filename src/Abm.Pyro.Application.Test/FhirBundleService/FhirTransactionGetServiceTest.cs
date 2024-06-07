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
// using Abm.Pyro.Domain.Support;
// using Abm.Pyro.Domain.Test.Factories;
// using Xunit;
// using Task = System.Threading.Tasks.Task;
//
// namespace Abm.Pyro.Application.Test.FhirBundleService;
//
// public class FhirTransactionGetServiceTest
// {
//     
//     private readonly Mock<IFhirBundleCommonSupport> FhirBundleCommonSupportMock;
//     private readonly Mock<IFhirReadHandler> FhirReadHandlerMock;
//     private readonly Mock<IFhirSearchHandler> FhirSearchHandlerMock;
//
//     private readonly Patient PatientResource;
//    
//     //Setup
//     protected FhirTransactionGetServiceTest()
//     {
//         FhirBundleCommonSupportMock = new Mock<IFhirBundleCommonSupport>();
//
//         PatientResource = TestResourceFactory.PatientResource.GetDonaldDuck();
//         FhirOptionalResourceResponse fhirOptionalResourceResponse = new FhirOptionalResourceResponse(
//             Resource: PatientResource,
//             HttpStatusCode: HttpStatusCode.OK,
//             Headers: new Dictionary<string, StringValues>()
//             { 
//                 {Domain.Support.HttpHeaderName.ETag, new StringValues(StringSupport.GetEtag(int.Parse(PatientResource.Meta.VersionId)))}
//             });
//         
//         FhirReadHandlerMock = new Mock<IFhirReadHandler>();
//         FhirReadHandlerMock.Setup(x =>
//             x.Handle(
//                 It.IsAny<string>(),
//                 It.IsAny<string>(),
//                 It.IsAny<CancellationToken>())).ReturnsAsync(fhirOptionalResourceResponse);
//         
//         FhirResourceResponse fhirResourceResponse = new FhirResourceResponse(
//             Resource: PatientResource,
//             HttpStatusCode: HttpStatusCode.Created,
//             Headers: new Dictionary<string, StringValues>()
//             { 
//                 {Domain.Support.HttpHeaderName.ETag, new StringValues(StringSupport.GetEtag(int.Parse(PatientResource.Meta.VersionId)))}
//             });
//         
//         FhirSearchHandlerMock = new Mock<IFhirSearchHandler>();
//         FhirSearchHandlerMock.Setup(x => 
//             x.Handle(
//                 It.IsAny<string>(), 
//                 It.IsAny<string>(), 
//                 It.IsAny<Dictionary<string,StringValues>>(), 
//                 It.IsAny<CancellationToken>())).ReturnsAsync(fhirResourceResponse);
//         
//         
//     }
//
//     public class ProcessGets : FhirTransactionGetServiceTest
//     {
//         [Fact]
//         public async Task ResourceGetById_IsOk()
//         {
//             string serviceBaseUrl = "https://service-base-url.com/fhir";
//             FhirBundleCommonSupport fhirBundleCommonSupport = new FhirBundleCommonSupport(FhirUriFactoryFactory.GetFhirUriFactory(serviceBaseUrl));
//             
//             //Arrange
//             var target = new FhirTransactionGetService(
//                 fhirBundleCommonSupport,
//                 FhirReadHandlerMock.Object,
//                 FhirSearchHandlerMock.Object);
//
//             
//             var entryList = new List<Bundle.EntryComponent>()
//             {
//                 new Bundle.EntryComponent()
//                 {
//                     Request = new Bundle.RequestComponent()
//                     {
//                         Method = Bundle.HTTPVerb.GET, 
//                         Url = $"Patient/{PatientResource.Id}"
//                     }
//                 }
//             };
//
//             var requestHeaders = new Dictionary<string, StringValues>();
//             
//             var cancellationTokenSource = new CancellationTokenSource();
//             
//             //Act
//             await target.ProcessGets(entryList, requestHeaders, cancellationTokenSource);
//             
//             //Verify
//             FhirReadHandlerMock.Verify(x => 
//                 x.Handle(
//                     It.IsAny<string>(), 
//                     It.IsAny<string>(), 
//                     It.IsAny<CancellationToken>()), 
//                 times: Times.Once);
//             
//             FhirSearchHandlerMock.Verify(x =>
//                     x.Handle(
//                         It.IsAny<string>(), 
//                         It.IsAny<string>(), 
//                         It.IsAny<Dictionary<string,StringValues>>(), 
//                         It.IsAny<CancellationToken>()), 
//                 times: Times.Never);
//             
//             //Assert
//             Assert.Equal(StringSupport.GetEtag(int.Parse(PatientResource.Meta.VersionId)), entryList.First().Response.Etag);
//             Assert.Equal(HttpStatusCode.OK.Display(), entryList.First().Response.Status);
//             Assert.Equal(PatientResource, entryList.First().Resource);
//             Assert.Equal($"{serviceBaseUrl}/{PatientResource.TypeName}/{PatientResource.Id}", entryList.First().FullUrl);
//         }
//         
//         [Fact]
//         public async Task ResourceByIdSearch_IsOk()
//         {
//             string serviceBaseUrl = "https://service-base-url.com/fhir";
//             FhirBundleCommonSupport fhirBundleCommonSupport = new FhirBundleCommonSupport(FhirUriFactoryFactory.GetFhirUriFactory(serviceBaseUrl));
//             
//             //Arrange
//             var target = new FhirTransactionGetService(
//                 fhirBundleCommonSupport,
//                 FhirReadHandlerMock.Object,
//                 FhirSearchHandlerMock.Object);
//
//             Identifier targetIhi = PatientResource.Identifier.Single(x => x.System.Equals("http://ns.electronichealth.net.au/id/hi/ihi/1.0"));
//             var entryList = new List<Bundle.EntryComponent>()
//             {
//                 new Bundle.EntryComponent()
//                 {
//                     Request = new Bundle.RequestComponent()
//                     {
//                         Method = Bundle.HTTPVerb.GET, 
//                         Url = $"Patient?identifier={targetIhi.System}|{targetIhi.Value}"
//                     }
//                 }
//             };
//
//             var requestHeaders = new Dictionary<string, StringValues>();
//             
//             var cancellationTokenSource = new CancellationTokenSource();
//             
//             //Act
//             await target.ProcessGets(entryList, requestHeaders, cancellationTokenSource);
//             
//             //Verify
//             FhirReadHandlerMock.Verify(x => 
//                     x.Handle(
//                         It.IsAny<string>(), 
//                         It.IsAny<string>(), 
//                         It.IsAny<CancellationToken>()), 
//                 times: Times.Never);
//             
//             FhirSearchHandlerMock.Verify(x =>
//                 x.Handle(
//                     It.IsAny<string>(), 
//                     It.IsAny<string>(), 
//                     It.IsAny<Dictionary<string,StringValues>>(), 
//                     It.IsAny<CancellationToken>()), 
//                 times: Times.Once);
//             
//             //Assert
//             Assert.Equal(StringSupport.GetEtag(int.Parse(PatientResource.Meta.VersionId)), entryList.First().Response.Etag);
//             Assert.Equal(HttpStatusCode.Created.Display(), entryList.First().Response.Status);
//             Assert.Equal(PatientResource, entryList.First().Resource);
//             Assert.Equal($"{serviceBaseUrl}/{PatientResource.TypeName}/{PatientResource.Id}", entryList.First().FullUrl);
//         }
//         
//     }
// }