using System.Net;
using Abm.Pyro.Domain.FhirSupport;

namespace Abm.Pyro.Domain.Validation;

public abstract class ValidatorBase<T>(IOperationOutcomeSupport operationOutcomeSupport) 
    : IValidatorBase<T> where T : IValidatable
{
    protected readonly List<string> FailureMessageList = [];

    public abstract ValidatorResult Validate(T item);

    protected ValidatorResult GetValidatorResult()
    {
        if (FailureMessageList.Count != 0)
        {
            return new ValidatorResult(
                isValid: false, 
                httpStatusCode: HttpStatusCode.BadRequest,
                operationOutcome: operationOutcomeSupport.GetError(messageList: FailureMessageList.ToArray()));    
        }
        
        return new ValidatorResult(isValid: true, httpStatusCode: null, operationOutcome: null);
        
    }
    
    protected ValidatorResult GetFailedEndpointPolicyValidatorResult()
    {
        return new ValidatorResult(
            isValid: false, 
            httpStatusCode: HttpStatusCode.Forbidden,
            operationOutcome: operationOutcomeSupport.GetError(messageList: new[]
            {
                "The server's endpoint policy controls have refused to authorize this request"
            }));
    }
}