using System.Net;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.Exceptions;

public class FhirWarnException : FhirException
{
  public FhirWarnException(HttpStatusCode httpStatusCode, string message)
    : base(httpStatusCode, message)
  {
  }
  public FhirWarnException(HttpStatusCode httpStatusCode, string message, Exception innerException)
    : base(httpStatusCode, message, innerException)
  {
  }
  public FhirWarnException(HttpStatusCode httpStatusCode, string[] messageList)
    : base(httpStatusCode, messageList)
  {
  }
  public FhirWarnException(HttpStatusCode httpStatusCode, string[] messageList, Exception innerException)
    : base(httpStatusCode, messageList, innerException)
  {
  }
  public FhirWarnException(HttpStatusCode httpStatusCode, OperationOutcome operationOutcome)
    : base(httpStatusCode: httpStatusCode, operationOutcome: operationOutcome)
  {
  }
  
  public FhirWarnException(HttpStatusCode httpStatusCode, string[] messageList, OperationOutcome operationOutcome)
    : base(httpStatusCode: httpStatusCode, messageList: messageList, operationOutcome: operationOutcome)
  {
  }
}
