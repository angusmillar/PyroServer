using System.Net;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.Exceptions;

public class FhirFatalException : FhirException
{
  public FhirFatalException(HttpStatusCode httpStatusCode, string message)
    : base(httpStatusCode, message)
  {
  }
  public FhirFatalException(HttpStatusCode httpStatusCode, string message, Exception innerException)
    : base(httpStatusCode, message, innerException)
  {
  }
  public FhirFatalException(HttpStatusCode httpStatusCode, string[] messageList)
    : base(httpStatusCode, messageList)
  {
  }
  public FhirFatalException(HttpStatusCode httpStatusCode, string[] messageList, Exception innerException)
    : base(httpStatusCode, messageList, innerException)
  {
  }
  
  public FhirFatalException(HttpStatusCode httpStatusCode, OperationOutcome operationOutcome)
    : base(httpStatusCode: httpStatusCode, operationOutcome: operationOutcome)
  {
  }
  
  public FhirFatalException(HttpStatusCode httpStatusCode, string[] messageList, OperationOutcome operationOutcome)
    : base(httpStatusCode: httpStatusCode, messageList: messageList, operationOutcome: operationOutcome)
  {
  }
}
