using System.Net;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.Validation;

public class ValidatorResult(
    bool isValid,
    //IEnumerable<string> failureMessageList,
    OperationOutcome? operationOutcome,
    HttpStatusCode? httpStatusCode)
{
    public bool IsValid { get; } = isValid;

    //public IEnumerable<string> FailureMessageList  { get; } = failureMessageList;
    public HttpStatusCode GetHttpStatusCode()
    {
        if (!IsValid && httpStatusCode is null)
        {
            throw new ApplicationException("Where ValidatorResult is not IsValid, a HttpStatusCode must be provided. Found null for HttpStatusCode");
        }
        
        ArgumentNullException.ThrowIfNull(httpStatusCode);
        
        return httpStatusCode.Value;
    }
    public OperationOutcome GetOperationOutcome()
    {
        if (IsValid)
        {
            throw new ApplicationException($"Can not construct an OperationOutcome instance where {nameof(IsValid)} is true.");
        }
        
        if (operationOutcome is null)
        {
            throw new NullReferenceException(nameof(operationOutcome));
        }

        return operationOutcome;
    }
    
}