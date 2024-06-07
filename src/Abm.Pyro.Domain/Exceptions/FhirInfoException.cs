using System.Net;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.Exceptions;

public class FhirInfoException : FhirException
{
  public FhirInfoException(HttpStatusCode httpStatusCode, string message)
    : base(httpStatusCode, message)
  {
  }
  public FhirInfoException(HttpStatusCode httpStatusCode, string message, Exception innerException)
    : base(httpStatusCode, message, innerException)
  {
  }
  public FhirInfoException(HttpStatusCode httpStatusCode, string[] messageList)
    : base(httpStatusCode, messageList)
  {
  }
  public FhirInfoException(HttpStatusCode httpStatusCode, string[] messageList, Exception innerException)
    : base(httpStatusCode, messageList, innerException)
  {
  }
  
  public FhirInfoException(HttpStatusCode httpStatusCode, OperationOutcome operationOutcome)
    : base(httpStatusCode: httpStatusCode, operationOutcome: operationOutcome)
  {
  }
  
  public FhirInfoException(HttpStatusCode httpStatusCode, string[] messageList, OperationOutcome operationOutcome)
    : base(httpStatusCode: httpStatusCode, messageList: messageList, operationOutcome: operationOutcome)
  {
  }
}
