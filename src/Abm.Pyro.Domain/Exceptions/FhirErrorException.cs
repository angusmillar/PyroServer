using System.Net;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.Exceptions;

public class FhirErrorException : FhirException
{
  public FhirErrorException(HttpStatusCode httpStatusCode, string message)
    : base(httpStatusCode, message)
  {
  }
  public FhirErrorException(HttpStatusCode httpStatusCode, string message, Exception innerException)
    : base(httpStatusCode, message, innerException)
  {
  }
  public FhirErrorException(HttpStatusCode httpStatusCode, string[] messageList)
    : base(httpStatusCode, messageList)
  {
  }
  public FhirErrorException(HttpStatusCode httpStatusCode, string[] messageList, Exception innerException)
    : base(httpStatusCode, messageList, innerException)
  {
  }
  
  public FhirErrorException(HttpStatusCode httpStatusCode, OperationOutcome operationOutcome)
    : base(httpStatusCode: httpStatusCode, operationOutcome: operationOutcome)
  {
  }
  
  public FhirErrorException(HttpStatusCode httpStatusCode, string[] messageList, OperationOutcome operationOutcome)
    : base(httpStatusCode: httpStatusCode, messageList: messageList, operationOutcome: operationOutcome)
  {
  }
}
